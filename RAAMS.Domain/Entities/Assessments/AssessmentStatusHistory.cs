using RAAMS.Domain.Common;
using RAAMS.Domain.Entities.Users;
using RAAMS.Domain.Enums;

namespace RAAMS.Domain.Entities.Assessments;

public class AssessmentStatusHistory : IEntity
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public AssessmentStatus? FromStatus { get; set; }
    public AssessmentStatus ToStatus { get; set; }
    public Guid ActorId { get; set; }
    public string? Reason { get; set; }
    public DateTime ChangedAt { get; set; }

    public Assessment Assessment { get; set; } = null!;
    public User Actor { get; set; } = null!;
}
