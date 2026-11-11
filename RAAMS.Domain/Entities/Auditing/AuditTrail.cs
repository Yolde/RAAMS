using RAAMS.Domain.Entities.Users;
using RAAMS.Domain.Enums;

namespace RAAMS.Domain.Entities.Auditing;

public class AuditTrail
{
    public long Id { get; set; }
    public string EntityType { get; set; } = null!;
    public Guid EntityId { get; set; }
    public AuditAction Action { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? ChangedFields { get; set; }
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public DateTime Timestamp { get; set; }
    public Guid? CorrelationId { get; set; }
    public string? Reason { get; set; }

    public User? User { get; set; }
}
