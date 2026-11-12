namespace RAAMS.Application.Assessments;

public class CreateAssessmentDto
{
    public string Name { get; set; } = null!;
    public string Scope { get; set; } = null!;
    public string Function { get; set; } = null!;
    public string Quarter { get; set; } = null!;
    public int Year { get; set; }
    public Guid LeadAuditorId { get; set; }
    public Guid CoordinatorId { get; set; }
    public string ReferenceNumber { get; set; } = null!;
}
