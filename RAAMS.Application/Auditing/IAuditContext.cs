namespace RAAMS.Application.Auditing;

public interface IAuditContext
{
    string? Reason { get; }
    string? CorrelationId { get; }
    Guid? UserId { get; }
    string? UserEmail { get; }
    void SetReason(string reason);
    void SetCorrelationId(string correlationId);
}
