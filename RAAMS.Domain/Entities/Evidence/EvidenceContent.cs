namespace RAAMS.Domain.Entities.Evidence;

public class EvidenceContent
{
    public Guid EvidenceId { get; set; }
    public byte[] Data { get; set; } = [];
    public EvidenceItem Evidence { get; set; } = null!;
}
