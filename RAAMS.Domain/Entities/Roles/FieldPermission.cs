using RAAMS.Domain.Common;

namespace RAAMS.Domain.Entities.Roles;

public class FieldPermission : IEntity
{
    public Guid Id { get; set; }
    public Guid RoleId { get; set; }
    public string EntityType { get; set; } = null!;
    public string FieldName { get; set; } = null!;
    public bool CanView { get; set; }
    public bool CanEdit { get; set; }

    public Role Role { get; set; } = null!;
}
