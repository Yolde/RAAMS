using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RAAMS.Application.Services;
using RAAMS.Domain.Common;
using RAAMS.Domain.Entities.Actions;
using RAAMS.Domain.Entities.Assessments;
using RAAMS.Domain.Entities.Auditing;
using RAAMS.Domain.Entities.Roles;
using RAAMS.Domain.Entities.Users;
using RAAMS.Infrastructure.Data;

namespace RAAMS.Tests;

public class RaamsDbContextTests
{
    private sealed class TestCurrentUser : ICurrentUserService
    {
        public Guid? UserId => null;
        public string? Email => null;
        public string? Role => null;
        public string? CorrelationId => null;
    }

    [Fact]
    public void SqlServerModelMapsKeysRelationshipsAndEnumColumns()
    {
        var options = new DbContextOptionsBuilder<RaamsDbContext>()
            .UseSqlServer("Server=localhost;Database=RAAMS_ModelOnly;Integrated Security=true;TrustServerCertificate=true")
            .Options;
        using var context = new RaamsDbContext(options, new TestCurrentUser());
        var model = context.Model;

        Assert.Equal(17, model.GetEntityTypes().Count());
        Assert.Equal("Actions", model.FindEntityType(typeof(ActionEntity))!.GetTableName());
        Assert.Equal("AssessmentStatusHistory", model.FindEntityType(typeof(AssessmentStatusHistory))!.GetTableName());
        Assert.Equal("AuditTrails", model.FindEntityType(typeof(AuditTrail))!.GetTableName());
        Assert.Equal(new[] { "RoleId", "PermissionId" }, model.FindEntityType(typeof(RolePermission))!.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.Equal(new[] { "UserId", "RoleId" }, model.FindEntityType(typeof(UserRole))!.FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.All(model.GetEntityTypes().SelectMany(e => e.GetProperties()), p => Assert.False(p.IsShadowProperty(), $"{p.DeclaringType.Name}.{p.Name} is shadow"));

        var action = model.FindEntityType(typeof(ActionEntity))!;
        var status = action.FindProperty(nameof(ActionEntity.Status))!;
        Assert.Equal(30, status.GetMaxLength());
        Assert.Equal(typeof(string), status.GetTypeMapping().Converter!.ProviderClrType);
        Assert.Equal(3, action.FindProperty(nameof(ActionEntity.CreatedDate))!.GetPrecision());
        Assert.True(action.FindProperty(nameof(ActionEntity.RowVersion))!.IsConcurrencyToken);
        Assert.All(action.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior));
        Assert.NotEmpty(action.GetDeclaredQueryFilters());
        Assert.Empty(model.FindEntityType(typeof(AuditTrail))!.GetDeclaredQueryFilters());
        Assert.All(model.GetEntityTypes().Where(e => typeof(ISoftDelete).IsAssignableFrom(e.ClrType)), e => Assert.NotEmpty(e.GetDeclaredQueryFilters()));
    }

    [Fact]
    public async Task SaveChangesSetsDatesAndSoftDeletesWithoutCascadingUserRoles()
    {
        var options = new DbContextOptionsBuilder<RaamsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new RaamsDbContext(options, new TestCurrentUser());
        var user = new User
        {
            Id = Guid.NewGuid(), EntraObjectId = Guid.NewGuid(), DisplayName = "Test",
            Email = "test@example.invalid", Function = "Test", Department = "Test",
            RowVersion = Array.Empty<byte>()
        };
        var role = new Role { Id = Guid.NewGuid(), Name = "Role" };
        context.AddRange(user, role, new UserRole { User = user, Role = role });
        var before = DateTime.UtcNow;
        await context.SaveChangesAsync();
        Assert.InRange(user.CreatedDate, before, DateTime.UtcNow);
        Assert.Equal(user.CreatedDate, user.ModifiedDate);

        user.DisplayName = "Changed";
        await context.SaveChangesAsync();
        Assert.True(user.ModifiedDate >= user.CreatedDate);
        var oldModified = user.ModifiedDate;
        context.ChangeTracker.AutoDetectChangesEnabled = false;
        context.Remove(user);
        await context.SaveChangesAsync();
        Assert.Empty(await context.Users.ToListAsync());
        var deleted = await context.Users.IgnoreQueryFilters().AsNoTracking().SingleAsync();
        Assert.True(deleted.IsDeleted);
        Assert.True(deleted.ModifiedDate >= oldModified);
        Assert.Single(await context.UserRoles.ToListAsync());
    }

    [Fact]
    public void SynchronousSaveAlsoSoftDeletes()
    {
        var options = new DbContextOptionsBuilder<RaamsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new RaamsDbContext(options, new TestCurrentUser());
        var delegation = new Delegation { Id = Guid.NewGuid(), Reason = "Test" };
        context.Delegations.Add(delegation);
        context.SaveChanges();
        context.Delegations.Remove(delegation);
        context.SaveChanges();
        Assert.Empty(context.Delegations);
        Assert.True(context.Delegations.IgnoreQueryFilters().Single().IsDeleted);
    }
}
