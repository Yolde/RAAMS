namespace RAAMS.Application.Authorization;

public interface IWorkflowAuthorizationService
{
    Task<WorkflowTransitionResult> CanTransitionAsync(
        string entityType, string currentStatus, string targetStatus,
        IEnumerable<string> userRoles, CancellationToken ct = default);

    Task<IEnumerable<string>> GetAllowedNextStatusesAsync(
        string entityType, string currentStatus,
        IEnumerable<string> userRoles, CancellationToken ct = default);
}
