using RAAMS.Application.Common;

namespace RAAMS.Application.Findings;

public class FindingFilterDto : PagedRequest
{
    public Guid? AssessmentId { get; set; }
    public string? Severity { get; set; }
    public string? Status { get; set; }
}
