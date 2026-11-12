namespace RAAMS.Application.Assessments;

public class ChangeAssessmentStatusDto
{
    public string TargetStatus { get; set; } = null!;
    public string? Reason { get; set; }
}
