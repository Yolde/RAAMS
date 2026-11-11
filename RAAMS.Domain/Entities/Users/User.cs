using RAAMS.Domain.Common;
using RAAMS.Domain.Entities.Actions;
using RAAMS.Domain.Entities.Assessments;
using RAAMS.Domain.Entities.Auditing;
using RAAMS.Domain.Entities.Evidence;
using RAAMS.Domain.Entities.Notifications;
using RAAMS.Domain.Entities.Reviews;
using RAAMS.Domain.Entities.Roles;

namespace RAAMS.Domain.Entities.Users;

public class User : IEntity, ICreatedEntity, IModifiedEntity, ISoftDelete, IHasRowVersion
{
    public Guid Id { get; set; }
    public Guid EntraObjectId { get; set; }
    public string DisplayName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Function { get; set; } = null!;
    public string Department { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; } = null!;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<Delegation> DelegationsGiven { get; set; } = new List<Delegation>();
    public ICollection<Delegation> DelegationsReceived { get; set; } = new List<Delegation>();
    public ICollection<Assessment> LeadAuditorAssessments { get; set; } = new List<Assessment>();
    public ICollection<Assessment> CoordinatorAssessments { get; set; } = new List<Assessment>();
    public ICollection<AssessmentStatusHistory> AssessmentStatusChanges { get; set; } = new List<AssessmentStatusHistory>();
    public ICollection<ActionEntity> ActionPartyActions { get; set; } = new List<ActionEntity>();
    public ICollection<ActionEntity> AuditeeActions { get; set; } = new List<ActionEntity>();
    public ICollection<ActionStatusHistory> ActionStatusChanges { get; set; } = new List<ActionStatusHistory>();
    public ICollection<EvidenceItem> UploadedEvidence { get; set; } = new List<EvidenceItem>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<AuditTrail> AuditTrails { get; set; } = new List<AuditTrail>();
}
