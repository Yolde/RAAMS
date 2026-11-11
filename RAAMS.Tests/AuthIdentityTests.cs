using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RAAMS.API.Controllers;
using RAAMS.API.Services;
using RAAMS.Application.Auth;
using RAAMS.Application.Services;
using RAAMS.Domain.Entities.Roles;
using RAAMS.Infrastructure.Data;

namespace RAAMS.Tests;

public class AuthIdentityTests
{
    [Fact]
    public async Task ProvisioningAndMeUseInternalUserIdForRolesAndPermissions()
    {
        var options = new DbContextOptionsBuilder<RaamsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), options => options.EnableNullChecks(false))
            .Options;
        var currentUser = Mock.Of<ICurrentUserService>();
        await using var db = new RaamsDbContext(options, currentUser);
        var service = new UserProvisioningService(db, NullLogger<UserProvisioningService>.Instance);
        var subject = Guid.NewGuid();
        var userId = await service.EnsureUserExistsAsync(subject, "test@example.invalid", "Test User");

        Assert.NotEqual(subject, userId);
        Assert.Equal(userId, await service.EnsureUserExistsAsync(subject, "test@example.invalid", "Test User"));
        var user = await db.Users.SingleAsync();
        Assert.Equal(subject, user.EntraObjectId);
        Assert.True(user.IsActive);
        Assert.Equal("Unknown", user.Function);
        Assert.Equal("Unknown", user.Department);

        var active = new Role { Id = Guid.NewGuid(), Name = "Auditee" };
        var future = new Role { Id = Guid.NewGuid(), Name = "Reviewer" };
        var expired = new Role { Id = Guid.NewGuid(), Name = "Sponsor" };
        var approve = new Permission { Id = Guid.NewGuid(), Name = "Action.Approve", Module = "Actions" };
        db.AddRange(active, future, expired, approve);
        db.UserRoles.AddRange(
            new UserRole { UserId = userId, RoleId = active.Id, EffectiveFrom = DateTime.UtcNow.AddDays(-1) },
            new UserRole { UserId = userId, RoleId = future.Id, EffectiveFrom = DateTime.UtcNow.AddDays(1) },
            new UserRole { UserId = userId, RoleId = expired.Id, EffectiveFrom = DateTime.UtcNow.AddDays(-2), EffectiveTo = DateTime.UtcNow.AddDays(-1) });
        db.RolePermissions.Add(new RolePermission { RoleId = active.Id, PermissionId = approve.Id });
        await db.SaveChangesAsync();

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("sub", subject.ToString()),
                new Claim("email", "test@example.invalid"),
                new Claim("name", "Test User"),
                new Claim("exp", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString())
            }, "test"))
        };
        var controller = new AuthController(db, service, currentUser)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
        var result = await controller.Me(CancellationToken.None);
        var response = Assert.IsType<MeResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(userId, response.UserId);
        Assert.Equal(new[] { "Auditee" }, response.Roles);
        Assert.Equal(new[] { "Action.Approve" }, response.Permissions);
        Assert.Equal(DateTimeKind.Utc, response.TokenExpiresAt.Kind);
    }
}
