namespace RAAMS.Application.Authorization;

public class FieldPermissionDto
{
    public Guid Id { get; set; }
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public string FieldName { get; set; } = null!;
    public bool CanView { get; set; }
    public bool CanEdit { get; set; }
}

public class UpsertFieldPermissionDto
{
    public Guid RoleId { get; set; }
    public string EntityType { get; set; } = null!;
    public string FieldName { get; set; } = null!;
    public bool CanView { get; set; }
    public bool CanEdit { get; set; }
}