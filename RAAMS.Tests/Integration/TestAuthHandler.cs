using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RAAMS.Infrastructure.Data;

namespace RAAMS.Tests.Integration;

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    RaamsDbContext db) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestScheme";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var subject = Request.Headers["X-Test-User-Id"].ToString();
        var roleHeader = Request.Headers["X-Test-Roles"].ToString();
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(roleHeader))
            return AuthenticateResult.NoResult();
        if (!Guid.TryParse(subject, out var subjectId) || subjectId == Guid.Empty)
            return AuthenticateResult.Fail("X-Test-User-Id must be a non-empty GUID.");
        var roles = roleHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (roles.Length == 0)
            return AuthenticateResult.NoResult();

        var claims = new List<Claim> { new("sub", subjectId.ToString()) };
        claims.AddRange(roles.Select(role => new Claim("role", role)));
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(
            item => item.EntraObjectId == subjectId, Context.RequestAborted);
        if (user is not null)
        {
            claims.Add(new Claim("email", user.Email));
            claims.Add(new Claim("name", user.DisplayName));
            claims.Add(new Claim("exp", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString()));
        }
        var identity = new ClaimsIdentity(claims, SchemeName, "name", "role");
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
