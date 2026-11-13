using RAAMS.Application.Authorization.Attributes;

namespace RAAMS.Application.Actions;

public class ActionDto
{
    public string ActionPartyName { get; set; } = null!;
    public Guid Id { get; set; }
    public Guid FindingId { get; set; }
    public string FindingReference { get; set; } = null!;
    public string ReferenceNumber { get; set; } = null!;
    public string Reportable { get; set; } = null!;
    public string ActionDescription { get; set; } = null!;
    public string ActionRequired { get; set; } = null!;
    public Guid ActionPartyId { get; set; }
    public Guid AuditeeId { get; set; }
    public string AuditeeName { get; set; } = null!;
    [MaskableProperty("TargetDate")]
    public DateOnly? TargetDate { get; set; }
    [MaskableProperty("ActionTaken")]
    public string? ActionTaken { get; set; }
    [MaskableProperty("Status")]
    public string? Status { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public DateTime? ClosedDate { get; set; }
    public DateTime CreatedDate { get; set; }
}
