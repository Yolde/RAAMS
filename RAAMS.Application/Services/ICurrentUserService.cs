namespace RAAMS.Application.Services;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    string? Role { get; }
    IEnumerable<string> Roles { get; }
    string? CorrelationId { get; }
}
