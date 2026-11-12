using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RAAMS.Application.Auditing;
using RAAMS.Application.Authorization;
using RAAMS.Application.Common;
using RAAMS.Application.Common.Exceptions;
using RAAMS.Application.Reviews;
using RAAMS.Application.Reviews.Validators;
using RAAMS.Application.Services;
using RAAMS.Domain.Entities.Actions;
using RAAMS.Domain.Entities.Reviews;
using RAAMS.Domain.Enums;
using RAAMS.Infrastructure.Data;

namespace RAAMS.API.Services;

public class ReviewService(
    RaamsDbContext db,
    IWorkflowAuthorizationService workflowAuth,
    ICurrentUserService currentUser,
    IAuditContext auditContext,
    ILogger<ReviewService> logger) : IReviewService
{
    private static readonly Expression<Func<Review, ReviewDto>> Projection = review => new ReviewDto
    {
        Id = review.Id,
        ActionId = review.ActionId,
        ActionReference = review.Action.ReferenceNumber,
        ReviewerId = review.ReviewerId,
        ReviewerName = review.Reviewer.DisplayName,
        Outcome = review.Outcome.ToString(),
        ReasonCode = review.ReasonCode.HasValue ? review.ReasonCode.Value.ToString() : null,
        Comments = review.Comments,
        ReviewedAt = review.ReviewedAt
    };

    // Reviews are retained history, including when their referenced action or reviewer is soft-deleted.
    private IQueryable<Review> Reviews() => db.Reviews.IgnoreQueryFilters().AsNoTracking();

    public async Task<PaginatedResult<ReviewDto>> ListAsync(Guid? actionId, int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 200 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new ValidationException("Page must be positive and PageSize must be between 1 and 200.");
        var query = Reviews();
        if (actionId is Guid id)
            query = query.Where(review => review.ActionId == id);
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(review => review.ReviewedAt).ThenBy(review => review.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(Projection).ToListAsync(ct);
        return new PaginatedResult<ReviewDto>(items, count, page, pageSize);
    }

    public Task<ReviewDto?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Reviews().Where(review => review.Id == id).Select(Projection).SingleOrDefaultAsync(ct);

    public async Task<ReviewDto> CreateAsync(CreateReviewDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var roles = currentUser.Roles.ToArray();
        if (!roles.Any(role => string.Equals(role, "Reviewer", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(role, "GRA", StringComparison.OrdinalIgnoreCase)))
            throw new ForbiddenException("Only reviewers and GRA can create reviews.");
        await new CreateReviewValidator().ValidateAndThrowAsync(dto, ct);

        if (currentUser.UserId is not Guid subject)
            throw new ForbiddenException("A registered user is required to create a review.");
        var reviewerId = await db.Users.AsNoTracking().Where(user => user.EntraObjectId == subject)
            .Select(user => user.Id).SingleOrDefaultAsync(ct);
        if (reviewerId == Guid.Empty)
            throw new ForbiddenException("A registered user is required to create a review.");

        var action = await db.Actions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Id == dto.ActionId && !item.IsDeleted, ct)
            ?? throw new ArgumentException("Action not found");
        if (action.Status != ActionStatus.Closed)
            throw new WorkflowViolationException("Can only review closed actions");

        var outcome = Enum.Parse<ReviewOutcome>(dto.Outcome);
        var now = DateTime.UtcNow;
        if (outcome == ReviewOutcome.Ineffective)
        {
            var transition = await workflowAuth.CanTransitionAsync("Action", "Closed", "Reopened", roles, ct);
            if (!transition.Allowed)
                throw new WorkflowViolationException(transition.Reason ?? "Transition not permitted");
            action.Status = ActionStatus.Reopened;
            db.ActionStatusHistories.Add(new ActionStatusHistory
            {
                Id = Guid.NewGuid(), ActionId = action.Id, FromStatus = ActionStatus.Closed,
                ToStatus = ActionStatus.Reopened, ActorId = reviewerId, Reason = dto.Comments, ChangedAt = now
            });
        }

        var review = new Review
        {
            Id = Guid.NewGuid(), ActionId = action.Id, ReviewerId = reviewerId, Outcome = outcome,
            ReasonCode = dto.ReasonCode is null ? null : Enum.Parse<ReviewReasonCode>(dto.ReasonCode),
            Comments = dto.Comments ?? string.Empty, ReviewedAt = now
        };
        db.Reviews.Add(review);
        auditContext.SetReason($"Effectiveness review: {dto.Outcome}" +
            (outcome == ReviewOutcome.Ineffective ? " — action reopened" : ""));
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created review {ReviewId} for action {ActionId}", review.Id, action.Id);
        return await GetByIdAsync(review.Id, ct)
            ?? throw new InvalidOperationException("The saved review could not be retrieved.");
    }
}
