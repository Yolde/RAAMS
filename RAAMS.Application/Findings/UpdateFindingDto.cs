namespace RAAMS.Application.Findings;

public class UpdateFindingDto
{
    public string Description { get; set; } = null!;
    public string RootCause { get; set; } = null!;
    public string Recommendation { get; set; } = null!;
    public string Severity { get; set; } = null!;
}
