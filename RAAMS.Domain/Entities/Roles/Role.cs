using RAAMS.Domain.Common;

namespace RAAMS.Domain.Entities.Roles;

public class Role : IEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<WorkflowPermission> WorkflowPermissions { get; set; } = new List<WorkflowPermission>();
    public ICollection<FieldPermission> FieldPermissions { get; set; } = new List<FieldPermission>();
}
