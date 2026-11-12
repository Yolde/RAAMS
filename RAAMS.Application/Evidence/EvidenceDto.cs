namespace RAAMS.Application.Evidence;

public class EvidenceDto
{
    public Guid Id { get; set; }
    public Guid ActionId { get; set; }
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }
    public int Version { get; set; }
    public string UploadedByName { get; set; } = null!;
    public DateTime UploadedAt { get; set; }
    public string? SharePointUrl { get; set; }
}
