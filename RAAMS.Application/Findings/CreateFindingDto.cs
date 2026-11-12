namespace RAAMS.Application.Findings;

public class CreateFindingDto
{
    public Guid AssessmentId { get; set; }
    public string ReferenceNumber { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string RootCause { get; set; } = null!;
    public string Recommendation { get; set; } = null!;
    public string Severity { get; set; } = null!;
}
