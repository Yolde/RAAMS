using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RAAMS.Application.Actions;
using RAAMS.Application.Actions.Validators;
using RAAMS.Application.Auditing;
using RAAMS.Application.Authorization;
using RAAMS.Application.Common;
using RAAMS.Application.Common.Exceptions;
using RAAMS.Application.Services;
using RAAMS.Domain.Entities.Actions;
using RAAMS.Domain.Enums;
using RAAMS.Infrastructure.Data;

namespace RAAMS.API.Services;

public class ActionService(
    RaamsDbContext db,
    IRecordSecurityService recordSecurity,
    IWorkflowAuthorizationService workflowAuth,
    ICurrentUserService currentUser,
    IAuditContext auditContext,
    ILogger<ActionService> logger) : IActionService
{
    private static readonly Expression<Func<ActionEntity, ActionDto>> Projection = action => new ActionDto
    {
        Id = action.Id,
        FindingId = action.FindingId,
        FindingReference = action.Finding.ReferenceNumber,
        ReferenceNumber = action.ReferenceNumber,
        ActionDescription = action.ActionDescription,
        ActionRequired = action.ActionRequired,
        ActionPartyId = action.ActionPartyId,
        ActionPartyName = action.ActionParty.DisplayName,
        AuditeeId = action.AuditeeId,
        AuditeeName = action.Auditee.DisplayName,
        TargetDate = action.TargetDate,
        ActionTaken = action.ActionTaken,
        Status = action.Status.ToString(),
        SubmittedDate = action.SubmittedDate,
        ApprovedDate = action.ApprovedDate,
        ClosedDate = action.ClosedDate,
        CreatedDate = action.CreatedDate
    };

    private static bool HasRole(IEnumerable<string> roles, string role) =>
        roles.Any(value => string.Equals(value, role, StringComparison.OrdinalIgnoreCase));

    private async Task<Guid> ResolveUserIdAsync(CancellationToken ct)
    {
        if (currentUser.UserId is not Guid subject)
            return Guid.Empty;
        return await db.Users.AsNoTracking().Where(user => user.EntraObjectId == subject)
            .Select(user => user.Id).SingleOrDefaultAsync(ct);
    }

    private IQueryable<ActionEntity> VisibleActions(string[] roles, Guid userId, bool tracked = false)
    {
        var query = db.Actions.Where(action => !action.IsDeleted &&
            !action.Finding.IsDeleted && !action.Finding.Assessment.IsDeleted);
        if (!tracked)
            query = query.AsNoTracking();
        return recordSecurity.ApplyActionFilter(query, roles, userId);
    }

    private async Task<ActionEntity> GetWritableAsync(Guid id, string[] roles, Guid userId, CancellationToken ct) =>
        await VisibleActions(roles, userId, tracked: true).SingleOrDefaultAsync(action => action.Id == id, ct)
        ?? throw new ForbiddenException("Action not found or not visible to the current user.");

    private async Task<ActionDto> GetSavedAsync(Guid id, CancellationToken ct) =>
        await GetByIdAsync(id, ct) ?? throw new InvalidOperationException("The saved action is not visible to the current user.");

    public async Task<PaginatedResult<ActionDto>> ListAsync(ActionFilterDto filter, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (filter.Page < 1 || filter.PageSize is < 1 or > 200 ||
            (long)(filter.Page - 1) * filter.PageSize > int.MaxValue)
            throw new ValidationException("Page must be positive and PageSize must be between 1 and 200.");

        var roles = currentUser.Roles.ToArray();
        var userId = await ResolveUserIdAsync(ct);
        var query = VisibleActions(roles, userId);
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            if (!Enum.TryParse<ActionStatus>(filter.Status, false, out var status) ||
                !Enum.IsDefined(status) || status.ToString() != filter.Status)
                throw new ValidationException("Status is not valid.");
            query = query.Where(action => action.Status == status);
        }
        if (filter.ActionPartyId is Guid actionPartyId)
            query = query.Where(action => action.ActionPartyId == actionPartyId);
        if (filter.AuditeeId is Guid auditeeId)
            query = query.Where(action => action.AuditeeId == auditeeId);
        if (filter.FindingId is Guid findingId)
            query = query.Where(action => action.FindingId == findingId);
        if (filter.OverdueOnly)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            query = query.Where(action => action.TargetDate < today &&
                (action.Status == ActionStatus.Open || action.Status == ActionStatus.Submitted ||
                 action.Status == ActionStatus.Reopened));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(action => action.TargetDate)
            .ThenByDescending(action => action.CreatedDate).ThenBy(action => action.Id)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(Projection).ToListAsync(ct);
        return new PaginatedResult<ActionDto>(items, total, filter.Page, filter.PageSize);
    }

    public async Task<ActionDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var roles = currentUser.Roles.ToArray();
        var userId = await ResolveUserIdAsync(ct);
        return await VisibleActions(roles, userId).Where(action => action.Id == id)
            .Select(Projection).SingleOrDefaultAsync(ct);
    }

    public async Task<ActionDto> CreateAsync(CreateActionDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var roles = currentUser.Roles.ToArray();
        if (!HasRole(roles, "GRA") && !HasRole(roles, "LeadAuditor"))
            throw new ForbiddenException("Only GRA and lead auditors can create actions.");
        if (dto.ActionPartyId == dto.AuditeeId)
            throw new WorkflowViolationException("Action Party and Auditee must be different users (segregation of duties)");
        await new CreateActionValidator().ValidateAndThrowAsync(dto, ct);

        var userId = await ResolveUserIdAsync(ct);
        var writerRoles = HasRole(roles, "GRA") ? new[] { "GRA" } : new[] { "LeadAuditor" };
        var assessmentIds = recordSecurity.ApplyAssessmentFilter(
            db.Assessments.Where(assessment => !assessment.IsDeleted), writerRoles, userId)
            .Select(assessment => assessment.Id);
        if (!await db.Findings.AnyAsync(finding => finding.Id == dto.FindingId && !finding.IsDeleted &&
                assessmentIds.Contains(finding.AssessmentId), ct))
            throw new ForbiddenException("Finding not found or not visible to the current user.");

        var action = new ActionEntity
        {
            Id = Guid.NewGuid(), FindingId = dto.FindingId, ReferenceNumber = dto.ReferenceNumber,
            ActionDescription = dto.ActionDescription, ActionRequired = dto.ActionRequired,
            ActionPartyId = dto.ActionPartyId, AuditeeId = dto.AuditeeId, TargetDate = dto.TargetDate,
            Status = ActionStatus.Open
        };
        db.Actions.Add(action);
        auditContext.SetReason($"Action created: {action.ReferenceNumber}");
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created action {ActionId}", action.Id);
        return await GetSavedAsync(action.Id, ct);
    }

    public async Task<ActionDto> UpdateAsync(Guid id, UpdateActionDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await new UpdateActionValidator().ValidateAndThrowAsync(dto, ct);
        var roles = currentUser.Roles.ToArray();
        var userId = await ResolveUserIdAsync(ct);
        var action = await GetWritableAsync(id, roles, userId, ct);
        if (action.Status != ActionStatus.Open)
            throw new WorkflowViolationException("Cannot edit action unless status is Open");
        var gra = HasRole(roles, "GRA");
        if (gra && (!dto.TargetDate.HasValue || !dto.ActionPartyId.HasValue || !dto.AuditeeId.HasValue))
            throw new ValidationException("GRA updates must include TargetDate, ActionPartyId and AuditeeId.");
        if (!gra &&
            ((dto.TargetDate.HasValue && action.TargetDate != dto.TargetDate.Value) ||
             (dto.ActionPartyId.HasValue && action.ActionPartyId != dto.ActionPartyId.Value) ||
             (dto.AuditeeId.HasValue && action.AuditeeId != dto.AuditeeId.Value)))
            throw new ForbiddenException("Only GRA can change the target date, action party or auditee.");
        var actionPartyId = dto.ActionPartyId ?? action.ActionPartyId;
        var auditeeId = dto.AuditeeId ?? action.AuditeeId;
        if (actionPartyId == auditeeId)
            throw new WorkflowViolationException("Action Party and Auditee must be different users (segregation of duties)");

        action.ActionDescription = dto.ActionDescription;
        action.ActionRequired = dto.ActionRequired;
        action.ActionPartyId = actionPartyId;
        action.AuditeeId = auditeeId;
        action.TargetDate = dto.TargetDate ?? action.TargetDate;
        auditContext.SetReason($"Action updated: {action.ReferenceNumber}");
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Updated action {ActionId}", id);
        return await GetSavedAsync(id, ct);
    }

    public async Task<ActionDto> SubmitAsync(Guid id, SubmitActionDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await new SubmitActionValidator().ValidateAndThrowAsync(dto, ct);
        return await TransitionAsync(id, ActionStatus.Submitted, "Submitted", "submitted", null,
            dto.ActionTaken, ct);
    }

    public async Task<ActionDto> ApproveAsync(Guid id, ApproveActionDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await new ApproveActionValidator().ValidateAndThrowAsync(dto, ct);
        return await TransitionAsync(id, ActionStatus.Approved, "Approved", "approved", dto.Comments, null, ct);
    }

    public async Task<ActionDto> RejectAsync(Guid id, RejectActionDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await new RejectActionValidator().ValidateAndThrowAsync(dto, ct);
        return await TransitionAsync(id, ActionStatus.Open, "Rejected", "rejected", dto.Comments, null, ct);
    }

    public async Task<ActionDto> ReopenAsync(Guid id, RejectActionDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await new RejectActionValidator().ValidateAndThrowAsync(dto, ct);
        return await TransitionAsync(id, ActionStatus.Reopened, "Reopened", "reopened", dto.Comments, null, ct);
    }

    private async Task<ActionDto> TransitionAsync(Guid id, ActionStatus target, string workflowTarget,
        string operation, string? reason, string? actionTaken, CancellationToken ct)
    {
        var roles = currentUser.Roles.ToArray();
        var userId = await ResolveUserIdAsync(ct);
        if (userId == Guid.Empty)
            throw new ForbiddenException("A registered user is required to change action status.");
        var action = await GetWritableAsync(id, roles, userId, ct);
        var gra = HasRole(roles, "GRA");
        var permitted = target switch
        {
            ActionStatus.Submitted => gra || (HasRole(roles, "ActionParty") && action.ActionPartyId == userId),
            ActionStatus.Approved or ActionStatus.Open => gra || (HasRole(roles, "Auditee") && action.AuditeeId == userId),
            ActionStatus.Reopened => gra || HasRole(roles, "Reviewer"),
            _ => false
        };
        if (!permitted)
            throw new ForbiddenException("The current user cannot perform this action transition.");

        var from = action.Status;
        var validSource = target switch
        {
            ActionStatus.Submitted => from is ActionStatus.Open or ActionStatus.Reopened,
            ActionStatus.Approved or ActionStatus.Open => from == ActionStatus.Submitted,
            ActionStatus.Reopened => from == ActionStatus.Closed,
            _ => false
        };
        if (!validSource)
            throw new WorkflowViolationException($"Cannot {operation} an action with status {from}.");
        var result = await workflowAuth.CanTransitionAsync("Action", from.ToString(), workflowTarget, roles, ct);
        if (!result.Allowed)
            throw new WorkflowViolationException(result.Reason ?? "Transition not permitted");

        action.Status = target;
        var now = DateTime.UtcNow;
        if (target == ActionStatus.Submitted)
        {
            action.ActionTaken = actionTaken;
            action.SubmittedDate = now;
        }
        if (target == ActionStatus.Approved)
            action.ApprovedDate = now;

        db.ActionStatusHistories.Add(new ActionStatusHistory
        {
            Id = Guid.NewGuid(), ActionId = action.Id, FromStatus = from, ToStatus = target,
            ActorId = userId, Reason = reason, ChangedAt = now
        });
        var auditReason = $"Action {operation}: {action.ReferenceNumber}. {reason ?? ""}";
        // Full comments remain in status history when they would exceed the audit column limit.
        auditContext.SetReason(auditReason.Length <= 500 ? auditReason : $"Action {operation}: {action.ReferenceNumber}");
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Action {ActionId} transitioned from {FromStatus} to {ToStatus}", id, from, target);
        return await GetSavedAsync(id, ct);
    }
}
