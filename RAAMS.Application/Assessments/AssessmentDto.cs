namespace RAAMS.Application.Assessments;

public class AssessmentDto
{
    public Guid Id { get; set; }
    public string ReferenceNumber { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Scope { get; set; } = null!;
    public string Function { get; set; } = null!;
    public string Quarter { get; set; } = null!;
    public int Year { get; set; }
    public string LineOfDefence { get; set; } = null!;
    public string Opinion { get; set; } = null!;
    public Guid LeadAuditorId { get; set; }
    public string LeadAuditorName { get; set; } = null!;
    public Guid CoordinatorId { get; set; }
    public string CoordinatorName { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime? CompletionDate { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
}
