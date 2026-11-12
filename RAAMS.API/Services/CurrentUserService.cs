using System.Security.Claims;
using RAAMS.Application.Services;

namespace RAAMS.API.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true
            ? httpContextAccessor.HttpContext.User
            : null;

    public Guid? UserId => Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public string? Email => User?.FindFirstValue(ClaimTypes.Email) ?? User?.FindFirstValue("email");
    public string? Role => User?.FindFirstValue("role") ?? User?.FindFirstValue(ClaimTypes.Role);
    public IEnumerable<string> Roles
    {
        get
        {
            var roles = User?.FindAll("role")
                .Concat(User.FindAll(ClaimTypes.Role))
                .Select(claim => claim.Value)
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? Array.Empty<string>();

            return roles.Length > 0 ? roles : Role is { } role ? new[] { role } : Array.Empty<string>();
        }
    }
    public string? CorrelationId => User?.FindFirstValue("correlation_id");
}
