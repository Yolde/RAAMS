using System.Security.Claims;
using RAAMS.Application.Auditing;

namespace RAAMS.API.Services;

public class AuditContext(IHttpContextAccessor? httpContextAccessor) : IAuditContext
{
    private readonly AsyncLocal<string?> _reason = new();
    private readonly AsyncLocal<string?> _correlationId = new();
    private readonly string _generatedCorrelationId = Guid.NewGuid().ToString();

    public string? Reason => _reason.Value;
    public string? CorrelationId
    {
        get
        {
            if (_correlationId.Value is not null)
                return _correlationId.Value;
            var traceIdentifier = httpContextAccessor?.HttpContext?.TraceIdentifier;
            return traceIdentifier is null ? null :
                Guid.TryParse(traceIdentifier, out var id) ? id.ToString() : _generatedCorrelationId;
        }
    }

    private ClaimsPrincipal? User =>
        httpContextAccessor?.HttpContext?.User.Identity?.IsAuthenticated == true
            ? httpContextAccessor.HttpContext.User
            : null;

    public Guid? UserId =>
        Guid.TryParse(User?.FindFirstValue("sub") ?? User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id : null;
    public string? UserEmail => User?.FindFirstValue("email") ?? User?.FindFirstValue(ClaimTypes.Email);

    public void SetReason(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        _reason.Value = reason;
    }

    public void SetCorrelationId(string correlationId)
    {
        ArgumentNullException.ThrowIfNull(correlationId);
        if (!Guid.TryParse(correlationId, out var id))
            throw new ArgumentException("Correlation ID must be a GUID.", nameof(correlationId));
        _correlationId.Value = id.ToString();
    }
}
