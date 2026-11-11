using RAAMS.Domain.Common;
using RAAMS.Domain.Entities.Actions;
using RAAMS.Domain.Entities.Users;
using RAAMS.Domain.Enums;

namespace RAAMS.Domain.Entities.Reviews;

public class Review : IEntity
{
    public Guid Id { get; set; }
    public Guid ActionId { get; set; }
    public Guid ReviewerId { get; set; }
    public ReviewOutcome Outcome { get; set; }
    public ReviewReasonCode? ReasonCode { get; set; }
    public string Comments { get; set; } = null!;
    public DateTime ReviewedAt { get; set; }

    public ActionEntity Action { get; set; } = null!;
    public User Reviewer { get; set; } = null!;
}
