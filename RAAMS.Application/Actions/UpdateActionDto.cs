namespace RAAMS.Application.Actions;

public class UpdateActionDto
{
    public string ActionDescription { get; set; } = null!;
    public string ActionRequired { get; set; } = null!;
    public Guid ActionPartyId { get; set; }
    public Guid AuditeeId { get; set; }
    public DateOnly TargetDate { get; set; }
}
