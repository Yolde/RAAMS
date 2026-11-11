using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RAAMS.Application.Auth;
using RAAMS.Application.Services;
using RAAMS.Infrastructure.Data;

namespace RAAMS.API.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public class AuthController : ControllerBase
{
    private readonly RaamsDbContext _dbContext;
    private readonly IUserProvisioningService _userProvisioningService;

    public AuthController(
        RaamsDbContext dbContext,
        IUserProvisioningService userProvisioningService,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _userProvisioningService = userProvisioningService;
        ArgumentNullException.ThrowIfNull(currentUserService);
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken ct)
    {
        // JWT bearer handlers can map standard claims to ClaimTypes names.
        var subject = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue("email") ?? User.FindFirstValue(ClaimTypes.Email);
        var displayName = User.FindFirstValue("name") ?? User.FindFirstValue(ClaimTypes.Name);
        var expiration = User.FindFirstValue("exp");

        if (!Guid.TryParse(subject, out var entraObjectId) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(displayName) ||
            !long.TryParse(expiration, NumberStyles.Integer, CultureInfo.InvariantCulture, out var expirationSeconds))
        {
            return Unauthorized();
        }

        DateTime tokenExpiresAt;
        try
        {
            tokenExpiresAt = DateTimeOffset.FromUnixTimeSeconds(expirationSeconds).UtcDateTime;
        }
        catch (ArgumentOutOfRangeException)
        {
            return Unauthorized();
        }

        var userId = await _userProvisioningService.EnsureUserExistsAsync(entraObjectId, email, displayName, ct);

        var user = await _dbContext.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            throw new InvalidOperationException("The provisioned user was not found or is deleted.");

        // Token roles take precedence; UserRoles is the fallback when none are supplied.
        var roles = User.FindAll("role")
            .Concat(User.FindAll(ClaimTypes.Role))
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (roles.Count == 0)
        {
            var now = DateTime.UtcNow;
            var activeRoleIds = _dbContext.UserRoles
                .Where(ur => ur.UserId == userId && ur.EffectiveFrom <= now &&
                             (ur.EffectiveTo == null || ur.EffectiveTo > now))
                .Select(ur => ur.RoleId);

            roles = await _dbContext.Roles.AsNoTracking()
                .Where(role => activeRoleIds.Contains(role.Id))
                .Select(role => role.Name)
                .ToListAsync(ct);
        }

        // Resolve canonical names with ordinal case-insensitive matching, independent of database collation.
        var knownRoleNames = await _dbContext.Roles.AsNoTracking()
            .Select(role => role.Name)
            .ToListAsync(ct);
        var matchingRoleNames = knownRoleNames
            .Where(name => roles.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var permissions = await _dbContext.RolePermissions.AsNoTracking()
            .Where(rp => matchingRoleNames.Contains(rp.Role.Name))
            .Select(rp => rp.Permission.Name)
            .Distinct()
            .ToListAsync(ct);

        return Ok(new MeResponse
        {
            UserId = userId,
            Email = email,
            DisplayName = displayName,
            Function = user.Function,
            Department = user.Department,
            Roles = roles,
            Permissions = permissions,
            TokenExpiresAt = tokenExpiresAt
        });
    }

    [HttpGet("ping")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Ping()
    {
        return Ok(new
        {
            status = "authenticated",
            userId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
            email = User.FindFirstValue("email") ?? User.FindFirstValue(ClaimTypes.Email)
        });
    }
}
