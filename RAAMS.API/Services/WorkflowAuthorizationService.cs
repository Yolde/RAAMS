using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RAAMS.Application.Authorization;
using RAAMS.Infrastructure.Data;

namespace RAAMS.API.Services;

public class WorkflowAuthorizationService(
    RaamsDbContext dbContext,
    ILogger<WorkflowAuthorizationService> logger) : IWorkflowAuthorizationService
{
    public async Task<WorkflowTransitionResult> CanTransitionAsync(
        string entityType, string currentStatus, string targetStatus,
        IEnumerable<string> userRoles, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentStatus);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetStatus);
        ArgumentNullException.ThrowIfNull(userRoles);
        ct.ThrowIfCancellationRequested();

        var roles = userRoles.ToArray();
        if (roles.Any(role => string.Equals(role, "GRA", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase)))
            return new WorkflowTransitionResult(true);

        var permissions = await dbContext.WorkflowPermissions.AsNoTracking()
            .Where(permission => permission.EntityType == entityType &&
                                 permission.CurrentStatus == currentStatus &&
                                 permission.TargetStatus == targetStatus &&
                                 roles.Contains(permission.Role.Name))
            .Select(permission => new
            {
                permission.CanEdit, permission.CanApprove, permission.CanReopen, permission.CanDelete
            })
            .ToListAsync(ct);

        if (permissions.Any(permission => permission.CanEdit || permission.CanApprove ||
                                          permission.CanReopen || permission.CanDelete))
            return new WorkflowTransitionResult(true);

        var reason = permissions.Count == 0
            ? "No workflow permission defined for this transition"
            : "User roles not authorized for this transition";
        logger.LogInformation("Workflow transition denied for {EntityType}: {CurrentStatus} to {TargetStatus}. {Reason}",
            entityType, currentStatus, targetStatus, reason);
        return new WorkflowTransitionResult(false, reason);
    }

    public async Task<IEnumerable<string>> GetAllowedNextStatusesAsync(
        string entityType, string currentStatus,
        IEnumerable<string> userRoles, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentStatus);
        ArgumentNullException.ThrowIfNull(userRoles);
        ct.ThrowIfCancellationRequested();

        var roles = userRoles.ToArray();
        return await dbContext.WorkflowPermissions.AsNoTracking()
            .Where(permission => permission.EntityType == entityType &&
                                 permission.CurrentStatus == currentStatus &&
                                 roles.Contains(permission.Role.Name) &&
                                 (permission.CanEdit || permission.CanApprove ||
                                  permission.CanReopen || permission.CanDelete))
            .Select(permission => permission.TargetStatus)
            .Distinct()
            .ToListAsync(ct);
    }
}
