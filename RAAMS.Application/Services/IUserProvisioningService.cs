namespace RAAMS.Application.Services;

public interface IUserProvisioningService
{
    Task<Guid> EnsureUserExistsAsync(
        Guid entraObjectId,
        string email,
        string displayName,
        CancellationToken ct = default);

    Task<Guid?> GetRaamsUserIdAsync(
        Guid entraObjectId,
        CancellationToken ct = default);
}