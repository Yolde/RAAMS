namespace RAAMS.Application.Authorization;

public interface IFieldPermissionService
{
    Task<FieldPermissionResult> GetAsync(
        string entityType,
        string fieldName,
        IEnumerable<string> userRoles,
        CancellationToken ct = default);
}

public record FieldPermissionResult(bool CanView, bool CanEdit);
