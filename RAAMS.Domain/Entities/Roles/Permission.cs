using RAAMS.Domain.Common;

namespace RAAMS.Domain.Entities.Roles;

public class Permission : IEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Module { get; set; } = null!;
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
