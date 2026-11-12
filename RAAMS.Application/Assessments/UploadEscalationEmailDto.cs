namespace RAAMS.Application.Assessments;

public class UploadEscalationEmailDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? Notes { get; set; }
}
