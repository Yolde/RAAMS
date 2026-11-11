using RAAMS.Domain.Common;
using RAAMS.Domain.Entities.Evidence;
using RAAMS.Domain.Entities.Findings;
using RAAMS.Domain.Entities.Reviews;
using RAAMS.Domain.Entities.Users;
using RAAMS.Domain.Enums;

namespace RAAMS.Domain.Entities.Actions;

public class ActionEntity : IEntity, ICreatedEntity, IModifiedEntity, ISoftDelete, IHasRowVersion
{
    public Guid Id { get; set; }
    public Guid FindingId { get; set; }
    public string ReferenceNumber { get; set; } = null!;
    public string ActionDescription { get; set; } = null!;
    public string ActionRequired { get; set; } = null!;
    public Guid ActionPartyId { get; set; }
    public Guid AuditeeId { get; set; }
    public DateOnly TargetDate { get; set; }
    public string? ActionTaken { get; set; }
    public ActionStatus Status { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; } = null!;

    public Finding Finding { get; set; } = null!;
    public User ActionParty { get; set; } = null!;
    public User Auditee { get; set; } = null!;
    public ICollection<ActionStatusHistory> StatusHistory { get; set; } = new List<ActionStatusHistory>();
    public ICollection<EvidenceItem> Evidence { get; set; } = new List<EvidenceItem>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}
