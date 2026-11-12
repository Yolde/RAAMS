namespace RAAMS.Application.Findings;

public class FindingDto
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public string AssessmentName { get; set; } = null!;
    public string ReferenceNumber { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string RootCause { get; set; } = null!;
    public string Recommendation { get; set; } = null!;
    public string Severity { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
}
