using RAAMS.Domain.Common;
using RAAMS.Domain.Entities.Findings;
using RAAMS.Domain.Entities.Users;
using RAAMS.Domain.Enums;

namespace RAAMS.Domain.Entities.Assessments;

public class Assessment : IEntity, ICreatedEntity, IModifiedEntity, ISoftDelete, IHasRowVersion
{
    public Guid Id { get; set; }
    public string ReferenceNumber { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Scope { get; set; } = null!;
    public string Function { get; set; } = null!;
    public string Quarter { get; set; } = null!;
    public int Year { get; set; }
    public Guid LeadAuditorId { get; set; }
    public Guid CoordinatorId { get; set; }
    public AssessmentStatus Status { get; set; }
    public DateTime? CompletionDate { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; } = null!;

    public User LeadAuditor { get; set; } = null!;
    public User Coordinator { get; set; } = null!;
    public ICollection<AssessmentStatusHistory> StatusHistory { get; set; } = new List<AssessmentStatusHistory>();
    public ICollection<Finding> Findings { get; set; } = new List<Finding>();
}
