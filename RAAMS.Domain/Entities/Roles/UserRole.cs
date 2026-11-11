using RAAMS.Domain.Entities.Users;

namespace RAAMS.Domain.Entities.Roles;

public class UserRole
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
