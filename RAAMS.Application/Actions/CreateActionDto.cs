namespace RAAMS.Application.Actions;

public class CreateActionDto
{
    public Guid FindingId { get; set; }
    public string ReferenceNumber { get; set; } = null!;
    public string Reportable { get; set; } = "NonCEO";
    public string ActionDescription { get; set; } = null!;
    public string ActionRequired { get; set; } = null!;
    public Guid ActionPartyId { get; set; }
    public Guid AuditeeId { get; set; }
    public DateOnly TargetDate { get; set; }
}
