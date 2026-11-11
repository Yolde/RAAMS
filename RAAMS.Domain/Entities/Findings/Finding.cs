using RAAMS.Domain.Common;
using RAAMS.Domain.Entities.Actions;
using RAAMS.Domain.Entities.Assessments;
using RAAMS.Domain.Enums;

namespace RAAMS.Domain.Entities.Findings;

public class Finding : IEntity, ICreatedEntity, IModifiedEntity, ISoftDelete, IHasRowVersion
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public string ReferenceNumber { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string RootCause { get; set; } = null!;
    public string Recommendation { get; set; } = null!;
    public FindingSeverity Severity { get; set; }
    public FindingStatus Status { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; } = null!;

    public Assessment Assessment { get; set; } = null!;
    public ICollection<ActionEntity> Actions { get; set; } = new List<ActionEntity>();
}
