namespace RAAMS.Application.Evidence;

public class CreateEvidenceDto
{
    public Guid ActionId { get; set; }
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }
}
