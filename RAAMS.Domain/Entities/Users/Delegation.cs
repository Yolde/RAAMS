using RAAMS.Domain.Common;

namespace RAAMS.Domain.Entities.Users;

public class Delegation : IEntity, ICreatedEntity, ISoftDelete
{
    public Guid Id { get; set; }
    public Guid DelegatorUserId { get; set; }
    public Guid DelegateUserId { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime EffectiveTo { get; set; }
    public string Reason { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
    public bool IsDeleted { get; set; }

    public User DelegatorUser { get; set; } = null!;
    public User DelegateUser { get; set; } = null!;
}
