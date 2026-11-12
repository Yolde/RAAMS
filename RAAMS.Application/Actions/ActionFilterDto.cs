using RAAMS.Application.Common;

namespace RAAMS.Application.Actions;

public class ActionFilterDto : PagedRequest
{
    public string? Status { get; set; }
    public Guid? ActionPartyId { get; set; }
    public Guid? AuditeeId { get; set; }
    public Guid? FindingId { get; set; }
    public bool OverdueOnly { get; set; }
}
