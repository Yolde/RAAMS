using Microsoft.EntityFrameworkCore;
using RAAMS.Application.Services;
using RAAMS.Domain.Entities.Users;
using RAAMS.Infrastructure.Data;

namespace RAAMS.API.Services;

public class UserProvisioningService : IUserProvisioningService
{
    private readonly RaamsDbContext _db;
    private readonly ILogger<UserProvisioningService> _logger;

    public UserProvisioningService(RaamsDbContext db, ILogger<UserProvisioningService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Guid> EnsureUserExistsAsync(
        Guid entraObjectId,
        string email,
        string displayName,
        CancellationToken ct = default)
    {
        var existing = await _db.Users
            .Where(u => u.EntraObjectId == entraObjectId && !u.IsDeleted)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(ct);

        if (existing.HasValue)
        {
            _logger.LogInformation(
                "User with EntraObjectId {EntraObjectId} already exists (RAAMS Id: {RaamsId})",
                entraObjectId, existing.Value);
            return existing.Value;
        }

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            EntraObjectId = entraObjectId,
            Email = email,
            DisplayName = displayName,
            Function = "Unknown",
            Department = "Unknown",
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow,
            IsDeleted = false
        };

        _db.Users.Add(newUser);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Provisioned new user. EntraObjectId: {EntraObjectId}, RAAMS Id: {RaamsId}, Email: {Email}",
            entraObjectId, newUser.Id, email);

        return newUser.Id;
    }

    public async Task<Guid?> GetRaamsUserIdAsync(Guid entraObjectId, CancellationToken ct = default)
    {
        return await _db.Users
            .AsNoTracking()
            .Where(u => u.EntraObjectId == entraObjectId && !u.IsDeleted)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(ct);
    }
}