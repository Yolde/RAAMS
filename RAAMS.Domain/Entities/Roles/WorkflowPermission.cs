using RAAMS.Domain.Common;

namespace RAAMS.Domain.Entities.Roles;

public class WorkflowPermission : IEntity
{
    public Guid Id { get; set; }
    public Guid RoleId { get; set; }
    public string EntityType { get; set; } = null!;
    public string CurrentStatus { get; set; } = null!;
    public string TargetStatus { get; set; } = null!;
    public bool CanView { get; set; }
    public bool CanEdit { get; set; }
    public bool CanApprove { get; set; }
    public bool CanReopen { get; set; }
    public bool CanDelete { get; set; }

    public Role Role { get; set; } = null!;
}
