namespace RAAMS.Application.Assessments;

public class EscalationEmailDto
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public string AssessmentReference { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}
