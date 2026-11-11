using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RAAMS.API.Services;
using RAAMS.Application.Authorization;
using RAAMS.Application.Auditing;
using RAAMS.Application.Services;
using RAAMS.Domain.Entities.Roles;
using RAAMS.Infrastructure.Data;
using RAAMS.Infrastructure.Auditing;

namespace RAAMS.Tests;

public class WorkflowAuthorizationServiceTests
{
    [Fact]
    public async Task EvaluatesPermissionsAndDistinctAllowedTargets()
    {
        var options = new DbContextOptionsBuilder<RaamsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new RaamsDbContext(options, Mock.Of<ICurrentUserService>(), Mock.Of<IAuditContext>(), NullLogger<AuditSaveChangesInterceptor>.Instance);
        var viewer = new Role { Id = Guid.NewGuid(), Name = "Viewer" };
        var editor = new Role { Id = Guid.NewGuid(), Name = "Editor" };
        var approver = new Role { Id = Guid.NewGuid(), Name = "Approver" };
        db.WorkflowPermissions.AddRange(
            Permission(viewer, "Submitted", canView: true),
            Permission(editor, "Submitted", canEdit: true),
            Permission(approver, "Submitted", canApprove: true),
            Permission(editor, "Reopened", canReopen: true),
            Permission(editor, "Deleted", canDelete: true));
        await db.SaveChangesAsync();
        var service = new WorkflowAuthorizationService(db, NullLogger<WorkflowAuthorizationService>.Instance);

        Assert.Equal(new WorkflowTransitionResult(false, "No workflow permission defined for this transition"),
            await service.CanTransitionAsync("Action", "Open", "Unknown", ["Viewer"]));
        Assert.Equal(new WorkflowTransitionResult(false, "User roles not authorized for this transition"),
            await service.CanTransitionAsync("Action", "Open", "Submitted", ["Viewer"]));
        Assert.True((await service.CanTransitionAsync("Action", "Open", "Submitted", ["Viewer", "Editor"])).Allowed);
        Assert.True((await service.CanTransitionAsync("Action", "Open", "Submitted", ["Approver"])).Allowed);
        Assert.True((await service.CanTransitionAsync("Action", "Open", "Reopened", ["Editor"])).Allowed);
        Assert.True((await service.CanTransitionAsync("Action", "Open", "Deleted", ["Editor"])).Allowed);
        Assert.False((await service.CanTransitionAsync("Assessment", "Open", "Submitted", ["Editor"])).Allowed);
        var targets = (await service.GetAllowedNextStatusesAsync("Action", "Open", ["Viewer", "Editor", "Approver"])).Order().ToArray();
        Assert.Equal(new[] { "Deleted", "Reopened", "Submitted" }, targets);
        Assert.Empty(await service.GetAllowedNextStatusesAsync("Action", "Open", ["Viewer"]));
        Assert.Empty(await service.GetAllowedNextStatusesAsync("Action", "Open", []));
    }

    [Fact]
    public async Task AdminOverrideIsSeparateFromConfiguredStatusDiscovery()
    {
        var options = new DbContextOptionsBuilder<RaamsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new RaamsDbContext(options, Mock.Of<ICurrentUserService>(), Mock.Of<IAuditContext>(), NullLogger<AuditSaveChangesInterceptor>.Instance);
        var service = new WorkflowAuthorizationService(db, NullLogger<WorkflowAuthorizationService>.Instance);

        Assert.True((await service.CanTransitionAsync("Action", "Open", "Unconfigured", ["gra"])).Allowed);
        Assert.True((await service.CanTransitionAsync("Action", "Open", "Unconfigured", ["superadmin"])).Allowed);
        Assert.Empty(await service.GetAllowedNextStatusesAsync("Action", "Open", ["GRA"]));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CanTransitionAsync("Action", "Open", "Submitted", ["GRA"], cancelled.Token));
    }

    private static WorkflowPermission Permission(
        Role role, string target, bool canView = false, bool canEdit = false,
        bool canApprove = false, bool canReopen = false, bool canDelete = false) =>
        new()
        {
            Id = Guid.NewGuid(), Role = role, EntityType = "Action", CurrentStatus = "Open",
            TargetStatus = target, CanView = canView, CanEdit = canEdit,
            CanApprove = canApprove, CanReopen = canReopen, CanDelete = canDelete
        };
}
