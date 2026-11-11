using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RAAMS.API.Services;
using RAAMS.Application.Authorization;
using RAAMS.Application.Services;
using RAAMS.Domain.Entities.Roles;
using RAAMS.Infrastructure.Data;

namespace RAAMS.Tests;

public class FieldPermissionServiceTests
{
    [Fact]
    public async Task CombinesRolesCachesCanonicalRoleSetAndDefaultsToDeny()
    {
        var options = new DbContextOptionsBuilder<RaamsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new RaamsDbContext(options, Mock.Of<ICurrentUserService>());
        var viewer = new Role { Id = Guid.NewGuid(), Name = "Viewer" };
        var editor = new Role { Id = Guid.NewGuid(), Name = "Editor" };
        db.FieldPermissions.AddRange(
            new FieldPermission { Id = Guid.NewGuid(), Role = viewer, EntityType = "Action", FieldName = "Status", CanView = true },
            new FieldPermission { Id = Guid.NewGuid(), Role = editor, EntityType = "Action", FieldName = "Status", CanEdit = true });
        await db.SaveChangesAsync();
        using var cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        var service = new FieldPermissionService(db, cache, NullLogger<FieldPermissionService>.Instance);

        Assert.Equal(new FieldPermissionResult(true, true), await service.GetAsync("Action", "Status", ["Viewer", "Editor"]));
        db.FieldPermissions.RemoveRange(db.FieldPermissions);
        await db.SaveChangesAsync();
        Assert.Equal(new FieldPermissionResult(true, true), await service.GetAsync("Action", "Status", ["Editor", "Viewer", "Viewer"]));
        Assert.Equal(new FieldPermissionResult(false, false), await service.GetAsync("Action", "Other", ["Editor", "Viewer"]));
        Assert.Equal(new FieldPermissionResult(false, false), await service.GetAsync("Action", "Status", []));
    }

    [Fact]
    public async Task AdminRolesBypassDatabaseAndOtherKeysDoNotCollide()
    {
        var options = new DbContextOptionsBuilder<RaamsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new RaamsDbContext(options, Mock.Of<ICurrentUserService>());
        var role = new Role { Id = Guid.NewGuid(), Name = "A|B" };
        db.FieldPermissions.Add(new FieldPermission
        {
            Id = Guid.NewGuid(), Role = role, EntityType = "Action", FieldName = "Status", CanView = true
        });
        await db.SaveChangesAsync();
        using var cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        var service = new FieldPermissionService(db, cache, NullLogger<FieldPermissionService>.Instance);

        Assert.Equal(new FieldPermissionResult(true, true), await service.GetAsync("Action", "Status", ["gra"]));
        Assert.Equal(new FieldPermissionResult(true, true), await service.GetAsync("Action", "Status", ["SuperAdmin"]));
        Assert.Equal(new FieldPermissionResult(true, false), await service.GetAsync("Action", "Status", ["A|B"]));
        Assert.Equal(new FieldPermissionResult(false, false), await service.GetAsync("Action", "Status", ["A", "B"]));
        Assert.Equal(new FieldPermissionResult(false, false), await service.GetAsync("User", "Status", ["A|B"]));
    }
}
