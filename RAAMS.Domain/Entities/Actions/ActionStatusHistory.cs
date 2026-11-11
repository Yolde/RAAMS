using RAAMS.Domain.Common;
using RAAMS.Domain.Entities.Users;
using RAAMS.Domain.Enums;

namespace RAAMS.Domain.Entities.Actions;

public class ActionStatusHistory : IEntity
{
    public Guid Id { get; set; }
    public Guid ActionId { get; set; }
    public ActionStatus? FromStatus { get; set; }
    public ActionStatus ToStatus { get; set; }
    public Guid ActorId { get; set; }
    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; }

    public ActionEntity Action { get; set; } = null!;
    public User Actor { get; set; } = null!;
}
