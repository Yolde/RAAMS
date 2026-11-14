namespace RAAMS.Application.Authorization;

public record FieldPermissionResult(bool CanView, bool CanEdit);

public interface IFieldPermissionService
{
    Task<FieldPermissionResult> GetAsync(
        string entityType,
        string fieldName,
        IEnumerable<string> userRoles,
        CancellationToken ct = default);

    Task<IReadOnlyList<FieldPermissionDto>> ListAllAsync(CancellationToken ct = default);
    Task<FieldPermissionDto> UpsertAsync(UpsertFieldPermissionDto dto, CancellationToken ct = default);
}