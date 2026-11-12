using RAAMS.Application.Common;

namespace RAAMS.Application.Assessments;

public class AssessmentFilterDto : PagedRequest
{
    public string? Status { get; set; }
    public string? Function { get; set; }
    public string? Quarter { get; set; }
    public int? Year { get; set; }
    public Guid? LeadAuditorId { get; set; }
}
