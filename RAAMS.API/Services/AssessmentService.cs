using FluentValidation;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using RAAMS.Application.Assessments;
using RAAMS.Application.Assessments.Validators;
using RAAMS.Application.Auditing;
using RAAMS.Application.Authorization;
using RAAMS.Application.Common;
using RAAMS.Application.Common.Exceptions;
using RAAMS.Application.Services;
using RAAMS.Domain.Entities.Assessments;
using RAAMS.Domain.Enums;
using RAAMS.Infrastructure.Data;

namespace RAAMS.API.Services;

public class AssessmentService(
    RaamsDbContext db,
    IRecordSecurityService recordSecurity,
    IWorkflowAuthorizationService workflowAuthorization,
    ICurrentUserService currentUser,
    IAuditContext auditContext,
    ILogger<AssessmentService> logger) : IAssessmentService
{
    private static readonly Expression<Func<Assessment, AssessmentDto>> AssessmentProjection = assessment => new()
    {
        Id = assessment.Id,
        ReferenceNumber = assessment.ReferenceNumber,
        Name = assessment.Name,
        Scope = assessment.Scope,
        Function = assessment.Function,
        Quarter = assessment.Quarter,
        Year = assessment.Year,
        LeadAuditorId = assessment.LeadAuditorId,
        LeadAuditorName = assessment.LeadAuditor.DisplayName,
        CoordinatorId = assessment.CoordinatorId,
        CoordinatorName = assessment.Coordinator.DisplayName,
        Status = assessment.Status.ToString(),
        CompletionDate = assessment.CompletionDate,
        CancellationReason = assessment.CancellationReason,
        CreatedDate = assessment.CreatedDate,
        ModifiedDate = assessment.ModifiedDate
    };

    private async Task<Guid> ResolveCurrentUserIdAsync(CancellationToken ct)
    {
        if (currentUser.UserId is not Guid subject)
            return Guid.Empty;

        return await db.Users.AsNoTracking()
            .Where(user => user.Id == subject || user.EntraObjectId == subject)
            .OrderBy(user => user.Id == subject ? 0 : 1)
            .Select(user => user.Id)
            .FirstOrDefaultAsync(ct);
    }

    private IQueryable<Assessment> VisibleAssessments(Guid userId) =>
        recordSecurity.ApplyAssessmentFilter(
            db.Assessments.AsNoTracking().Where(assessment => !assessment.IsDeleted),
            currentUser.Roles, userId);

    private IQueryable<Assessment> EditableAssessments(Guid userId) =>
        recordSecurity.ApplyAssessmentFilter(
            db.Assessments.Where(assessment => !assessment.IsDeleted),
            currentUser.Roles, userId);

    public async Task<PaginatedResult<AssessmentDto>> ListAsync(AssessmentFilterDto filter, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (filter.Page < 1 || filter.PageSize is < 1 or > 200)
            throw new ValidationException("Page must be positive and PageSize must be between 1 and 200.");

        var userId = await ResolveCurrentUserIdAsync(ct);
        var query = VisibleAssessments(userId);
        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(assessment => assessment.Status.ToString() == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.Function))
            query = query.Where(assessment => assessment.Function == filter.Function);
        if (!string.IsNullOrWhiteSpace(filter.Quarter))
            query = query.Where(assessment => assessment.Quarter == filter.Quarter);
        if (filter.Year is int year)
            query = query.Where(assessment => assessment.Year == year);
        if (filter.LeadAuditorId is Guid leadAuditorId)
            query = query.Where(assessment => assessment.LeadAuditorId == leadAuditorId);

        var totalCount = await query.CountAsync(ct);
        var items = await query.OrderByDescending(assessment => assessment.CreatedDate)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(AssessmentProjection)
            .ToListAsync(ct);

        return new PaginatedResult<AssessmentDto>(items, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<AssessmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var userId = await ResolveCurrentUserIdAsync(ct);
        return await VisibleAssessments(userId)
            .Where(assessment => assessment.Id == id)
            .Select(AssessmentProjection)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<AssessmentDto> CreateAsync(CreateAssessmentDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await new CreateAssessmentValidator().ValidateAndThrowAsync(dto, ct);

        var sequence = await db.Assessments.IgnoreQueryFilters()
            .CountAsync(assessment => assessment.Year == dto.Year && assessment.Quarter == dto.Quarter, ct) + 1;
        var now = DateTime.UtcNow;
        var entity = new Assessment
        {
            Id = Guid.NewGuid(),
            ReferenceNumber = $"RA-{dto.Year}-{dto.Quarter}-{sequence:0000}",
            Name = dto.Name,
            Scope = dto.Scope,
            Function = dto.Function,
            Quarter = dto.Quarter,
            Year = dto.Year,
            LeadAuditorId = dto.LeadAuditorId,
            CoordinatorId = dto.CoordinatorId,
            Status = AssessmentStatus.Planned,
            CreatedDate = now,
            ModifiedDate = now,
            IsDeleted = false
        };
        db.Assessments.Add(entity);
        auditContext.SetReason($"Assessment created: {dto.Name}");
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created assessment {AssessmentId} with reference {ReferenceNumber}",
            entity.Id, entity.ReferenceNumber);

        return await GetByIdAsync(entity.Id, ct)
               ?? throw new InvalidOperationException("The created assessment is not visible to its creator.");
    }

    public async Task<AssessmentDto> UpdateAsync(Guid id, UpdateAssessmentDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await new UpdateAssessmentValidator().ValidateAndThrowAsync(dto, ct);
        var userId = await ResolveCurrentUserIdAsync(ct);
        var entity = await EditableAssessments(userId).SingleOrDefaultAsync(assessment => assessment.Id == id, ct)
                     ?? throw new ForbiddenException("Assessment not found or not visible to the current user.");

        entity.Name = dto.Name;
        entity.Scope = dto.Scope;
        entity.Function = dto.Function;
        entity.Quarter = dto.Quarter;
        entity.Year = dto.Year;
        entity.LeadAuditorId = dto.LeadAuditorId;
        entity.CoordinatorId = dto.CoordinatorId;
        auditContext.SetReason($"Assessment updated: {dto.Name}");
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Updated assessment {AssessmentId}", id);

        return await GetByIdAsync(id, ct)
               ?? throw new InvalidOperationException("The updated assessment is not visible to its editor.");
    }

    public async Task<AssessmentDto> ChangeStatusAsync(Guid id, ChangeAssessmentStatusDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await new ChangeAssessmentStatusValidator().ValidateAndThrowAsync(dto, ct);
        var userId = await ResolveCurrentUserIdAsync(ct);
        var entity = await EditableAssessments(userId).SingleOrDefaultAsync(assessment => assessment.Id == id, ct)
                     ?? throw new ForbiddenException("Assessment not found or not visible to the current user.");
        var currentStatus = entity.Status.ToString();
        var result = await workflowAuthorization.CanTransitionAsync(
            "Assessment", currentStatus, dto.TargetStatus, currentUser.Roles, ct);
        if (!result.Allowed)
            throw new WorkflowViolationException(result.Reason ?? "Transition not permitted");

        if (!Enum.TryParse<AssessmentStatus>(dto.TargetStatus, ignoreCase: false, out var targetStatus) ||
            !Enum.IsDefined(targetStatus))
            throw new ValidationException("TargetStatus is not a valid assessment status.");

        if (targetStatus == AssessmentStatus.Completed)
            entity.CompletionDate = DateTime.UtcNow;
        if (targetStatus == AssessmentStatus.Cancelled)
            entity.CancellationReason = dto.Reason;
        entity.Status = targetStatus;
        auditContext.SetReason($"Assessment status: {currentStatus} -> {dto.TargetStatus}. {dto.Reason ?? string.Empty}");
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Changed assessment {AssessmentId} status from {FromStatus} to {ToStatus}",
            id, currentStatus, dto.TargetStatus);

        return await GetByIdAsync(id, ct)
               ?? throw new InvalidOperationException("The changed assessment is not visible to its editor.");
    }

}
