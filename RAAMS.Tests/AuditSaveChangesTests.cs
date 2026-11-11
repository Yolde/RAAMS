using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RAAMS.API.Services;
using RAAMS.Application.Services;
using RAAMS.Domain.Entities.Roles;
using RAAMS.Domain.Entities.Users;
using RAAMS.Domain.Enums;
using RAAMS.Infrastructure.Auditing;
using RAAMS.Infrastructure.Data;

namespace RAAMS.Tests;

public class AuditSaveChangesTests
{
    [Fact]
    public async Task AuditsUserCreationUpdateAndSoftDeleteWithInternalUserId()
    {
        var subject = Guid.NewGuid();
        var correlation = Guid.NewGuid();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", subject.ToString()), new Claim("email", "test@example.invalid")
        }, "test"));
        var auditContext = new AuditContext(accessor);
        auditContext.SetReason("test reason");
        auditContext.SetCorrelationId(correlation.ToString());
        var options = new DbContextOptionsBuilder<RaamsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), o => o.EnableNullChecks(false))
            .Options;
        await using var db = new RaamsDbContext(options, Mock.Of<ICurrentUserService>(), auditContext,
            NullLogger<AuditSaveChangesInterceptor>.Instance);
        var user = new User
        {
            Id = Guid.NewGuid(), EntraObjectId = subject, Email = "test@example.invalid",
            DisplayName = "Initial", Function = "F", Department = "D"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var insert = await db.AuditTrails.SingleAsync();
        Assert.Equal(AuditAction.Insert, insert.Action);
        Assert.Equal(user.Id, insert.EntityId);
        Assert.Equal(user.Id, insert.UserId);
        Assert.NotEqual(subject, insert.UserId);
        Assert.Equal(correlation, insert.CorrelationId);
        Assert.Equal("test reason", insert.Reason);
        Assert.Equal("test@example.invalid", insert.UserEmail);
        Assert.Null(insert.OldValue);
        using var snapshot = JsonDocument.Parse(insert.NewValue!);
        Assert.False(snapshot.RootElement.TryGetProperty("RowVersion", out _));
        Assert.False(snapshot.RootElement.TryGetProperty("CreatedDate", out _));

        user.DisplayName = "Updated";
        await db.SaveChangesAsync();
        var update = await db.AuditTrails.SingleAsync(a => a.Action == AuditAction.Update);
        Assert.Equal("DisplayName", update.ChangedFields);
        Assert.Contains("Initial", update.OldValue);
        Assert.Contains("Updated", update.NewValue);

        db.Users.Remove(user);
        db.SaveChanges();
        var delete = await db.AuditTrails.OrderByDescending(a => a.Id).FirstAsync();
        Assert.Equal(AuditAction.Update, delete.Action);
        Assert.Equal("IsDeleted", delete.ChangedFields);
        Assert.True(user.IsDeleted);
        Assert.Equal(3, await db.AuditTrails.CountAsync());
    }

    [Fact]
    public async Task HardDeleteAndUnknownSubjectAreAuditedWithoutUserForeignKey()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) }, "test"));
        var options = new DbContextOptionsBuilder<RaamsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new RaamsDbContext(options, Mock.Of<ICurrentUserService>(), new AuditContext(accessor),
            NullLogger<AuditSaveChangesInterceptor>.Instance);
        var role = new Role { Id = Guid.NewGuid(), Name = "Temporary" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        db.Roles.Remove(role);
        await db.SaveChangesAsync();
        var delete = await db.AuditTrails.SingleAsync(a => a.Action == AuditAction.Delete);
        Assert.Equal(role.Id, delete.EntityId);
        Assert.Null(delete.UserId);
        Assert.Null(delete.NewValue);
    }
}
