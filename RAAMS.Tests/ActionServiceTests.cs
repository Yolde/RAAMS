using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RAAMS.API.Services;
using RAAMS.Application.Actions;
using RAAMS.Application.Auditing;
using RAAMS.Application.Authorization;
using RAAMS.Application.Common.Exceptions;
using RAAMS.Application.Services;
using RAAMS.Domain.Entities.Actions;
using RAAMS.Domain.Entities.Assessments;
using RAAMS.Domain.Entities.Findings;
using RAAMS.Domain.Entities.Users;
using RAAMS.Domain.Enums;
using RAAMS.Infrastructure.Auditing;
using RAAMS.Infrastructure.Data;

namespace RAAMS.Tests;

public class ActionServiceTests
{
    [Fact]
    public async Task ListsOnlyVisibleActionsWithFiltersOrderingAndOverdueBoundary()
    {
        await using var f = new Fixture();
        await f.SeedAsync();
        f.As(f.Party, "ActionParty");
        var page = await f.Service.ListAsync(new ActionFilterDto { OverdueOnly = true });
        Assert.Equal(new[] { f.Overdue.Id }, page.Items.Select(x => x.Id));
        Assert.Null(await f.Service.GetByIdAsync(f.OtherAction.Id));
        Assert.NotNull(await f.Service.GetByIdAsync(f.Overdue.Id));
        f.As(f.Lead, "GRA");
        var all = await f.Service.ListAsync(new ActionFilterDto { PageSize = 1, Page = 2 });
        Assert.Equal(3, all.TotalCount);
        Assert.Equal(f.Today.Id, Assert.Single(all.Items).Id);
        Assert.Single((await f.Service.ListAsync(new ActionFilterDto { Status = "Submitted", FindingId = f.Finding.Id })).Items);
        await Assert.ThrowsAsync<ValidationException>(() => f.Service.ListAsync(new ActionFilterDto { Status = "42" }));
        await Assert.ThrowsAsync<ValidationException>(() => f.Service.ListAsync(new ActionFilterDto { Page = int.MaxValue, PageSize = 200 }));
    }

    [Fact]
    public async Task CreateRequiresWriterAndOwnParentEvenWithMultipleRoles()
    {
        await using var f = new Fixture();
        await f.SeedAsync();
        f.As(f.Lead, "LeadAuditor", "AssuranceCoordinator");
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.CreateAsync(f.Create(f.OtherFinding.Id)));
        var equal = f.Create(f.Finding.Id);
        equal.AuditeeId = equal.ActionPartyId;
        var error = await Assert.ThrowsAsync<WorkflowViolationException>(() => f.Service.CreateAsync(equal));
        Assert.Equal("Action Party and Auditee must be different users (segregation of duties)", error.Message);
        var input = f.Create(f.Finding.Id);
        Assert.Equal("NonCEO", input.Reportable);
        input.Reportable = "CEO";
        var created = await f.Service.CreateAsync(input);
        Assert.Equal("Open", created.Status);
        Assert.Equal("CEO", created.Reportable);
        Assert.Equal("OWN", created.FindingReference);
        f.As(f.Lead, "SuperAdmin");
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.CreateAsync(f.Create(f.Finding.Id)));
    }

    [Fact]
    public async Task UpdateProtectsAssignmentAndDateAndEnforcesOpenAndSegregation()
    {
        await using var f = new Fixture();
        await f.SeedAsync();
        f.As(f.Party, "ActionParty");
        var update = f.Update(f.Overdue);
        update.ActionDescription = "Updated";
        update.Reportable = "CEO";
        Assert.Equal("Updated", (await f.Service.UpdateAsync(f.Overdue.Id, update)).ActionDescription);
        Assert.Equal(Reportable.CEO, f.Overdue.Reportable);
        update.TargetDate = update.TargetDate!.Value.AddDays(1);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.UpdateAsync(f.Overdue.Id, update));
        update = new UpdateActionDto { ActionDescription = "Without date", ActionRequired = "Required" };
        var partial = await f.Service.UpdateAsync(f.Overdue.Id, update);
        Assert.Equal("Without date", partial.ActionDescription);
        Assert.Equal("CEO", partial.Reportable);
        Assert.Equal(f.Overdue.TargetDate, partial.TargetDate);
        update = f.Update(f.Overdue);
        update.AuditeeId = update.ActionPartyId;
        await Assert.ThrowsAsync<ValidationException>(() => f.Service.UpdateAsync(f.Overdue.Id, update));
        await Assert.ThrowsAsync<WorkflowViolationException>(() => f.Service.UpdateAsync(f.Today.Id, f.Update(f.Today)));
        f.As(f.Lead, "GRA");
        await Assert.ThrowsAsync<ValidationException>(() => f.Service.UpdateAsync(f.Overdue.Id,
            new UpdateActionDto { ActionDescription = "D", ActionRequired = "R" }));
        update = f.Update(f.Overdue);
        update.TargetDate = update.TargetDate!.Value.AddDays(5);
        Assert.Equal(update.TargetDate, (await f.Service.UpdateAsync(f.Overdue.Id, update)).TargetDate);
    }

    [Fact]
    public async Task TransitionsUseWorkflowAndRecordActualStatusWithRegisteredActor()
    {
        await using var f = new Fixture();
        await f.SeedAsync();
        f.As(f.Party, "ActionParty");
        Assert.Equal("Submitted", (await f.Service.SubmitAsync(f.Overdue.Id, new SubmitActionDto { ActionTaken = "Done" })).Status);
        f.As(f.Auditee, "Auditee");
        var comments = new string('x', 500);
        Assert.Equal("Open", (await f.Service.RejectAsync(f.Overdue.Id, new RejectActionDto { Comments = comments })).Status);
        f.Workflow.Verify(w => w.CanTransitionAsync("Action", "Submitted", "Rejected",
            It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
        var rejection = await f.Db.ActionStatusHistories.SingleAsync(h => h.FromStatus == ActionStatus.Submitted && h.ToStatus == ActionStatus.Open);
        Assert.Equal(f.Auditee.Id, rejection.ActorId);
        Assert.Equal(comments, rejection.Reason);
        f.Audit.Verify(a => a.SetReason(It.Is<string>(s => s.Length <= 500 && s.StartsWith("Action rejected:"))), Times.Once);
        f.As(f.Party, "ActionParty");
        await f.Service.SubmitAsync(f.Overdue.Id, new SubmitActionDto { ActionTaken = "Done again" });
        f.As(f.Auditee, "Auditee");
        var approved = await f.Service.ApproveAsync(f.Overdue.Id, new ApproveActionDto { Comments = "approved" });
        Assert.Equal("Approved", approved.Status);
        Assert.NotNull(approved.ApprovedDate);
        f.Overdue.Status = ActionStatus.Closed;
        await f.Db.SaveChangesAsync();
        f.As(f.Lead, "Reviewer");
        Assert.Equal("Reopened", (await f.Service.ReopenAsync(f.Overdue.Id, new RejectActionDto { Comments = "again" })).Status);
        var histories = await f.Db.ActionStatusHistories.Where(h => h.ActionId == f.Overdue.Id).ToListAsync();
        Assert.Equal(5, histories.Count);
        Assert.Contains(histories, h => h.FromStatus == ActionStatus.Closed && h.ToStatus == ActionStatus.Reopened && h.ActorId == f.Lead.Id);
    }

    [Fact]
    public async Task TransitionsRejectWrongOwnerWrongRoleMissingActorAndDeniedWorkflow()
    {
        await using var f = new Fixture();
        await f.SeedAsync();
        f.As(f.Lead, "ActionParty", "GRA");
        // GRA is explicitly allowed, but a missing registered identity must not create invalid history.
        f.Current.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.SubmitAsync(f.Overdue.Id, new SubmitActionDto { ActionTaken = "Done" }));
        f.As(f.Auditee, "Auditee", "ActionParty");
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.SubmitAsync(f.Overdue.Id, new SubmitActionDto { ActionTaken = "Done" }));
        f.As(f.Party, "ActionParty", "Auditee");
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.ApproveAsync(f.Today.Id, new ApproveActionDto()));
        f.As(f.Party, "ActionParty");
        f.Workflow.Setup(w => w.CanTransitionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkflowTransitionResult(false, "Denied"));
        await Assert.ThrowsAsync<WorkflowViolationException>(() => f.Service.SubmitAsync(f.Overdue.Id, new SubmitActionDto { ActionTaken = "Done" }));
        Assert.Equal(ActionStatus.Open, f.Overdue.Status);
        Assert.Empty(await f.Db.ActionStatusHistories.ToListAsync());
    }

    [Fact]
    public async Task DeletedParentsAreNotVisibleEvenToGra()
    {
        await using var f = new Fixture();
        await f.SeedAsync();
        f.As(f.Lead, "GRA");
        f.Finding.IsDeleted = true;
        await f.Db.SaveChangesAsync();
        Assert.Null(await f.Service.GetByIdAsync(f.Overdue.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.CreateAsync(f.Create(f.Finding.Id)));
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.UpdateAsync(f.Overdue.Id, f.Update(f.Overdue)));
        Assert.Single((await f.Service.ListAsync(new ActionFilterDto())).Items);
    }

    [Fact]
    public async Task ReopenedActionsCanBeResubmittedButCannotBeEdited()
    {
        await using var f = new Fixture();
        await f.SeedAsync();
        f.Overdue.Status = ActionStatus.Reopened;
        await f.Db.SaveChangesAsync();
        f.As(f.Party, "ActionParty");
        await Assert.ThrowsAsync<WorkflowViolationException>(() => f.Service.UpdateAsync(f.Overdue.Id, f.Update(f.Overdue)));
        var result = await f.Service.SubmitAsync(f.Overdue.Id, new SubmitActionDto { ActionTaken = "Resubmitted" });
        Assert.Equal("Submitted", result.Status);
        Assert.Contains(await f.Db.ActionStatusHistories.ToListAsync(),
            h => h.FromStatus == ActionStatus.Reopened && h.ToStatus == ActionStatus.Submitted && h.ActorId == f.Party.Id);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public User Lead { get; } = MakeUser("lead");
        public User Party { get; } = MakeUser("party");
        public User Auditee { get; } = MakeUser("auditee");
        public User Other { get; } = MakeUser("other");
        public Mock<ICurrentUserService> Current { get; } = new();
        public Mock<IAuditContext> Audit { get; } = new();
        public Mock<IWorkflowAuthorizationService> Workflow { get; } = new();
        public RaamsDbContext Db { get; }
        public ActionService Service { get; }
        public Finding Finding { get; }
        public Finding OtherFinding { get; }
        public ActionEntity Overdue { get; }
        public ActionEntity Today { get; }
        public ActionEntity OtherAction { get; }

        public Fixture()
        {
            var options = new DbContextOptionsBuilder<RaamsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString(), o => o.EnableNullChecks(false)).Options;
            Db = new RaamsDbContext(options, Current.Object, Audit.Object, NullLogger<AuditSaveChangesInterceptor>.Instance);
            Workflow.Setup(w => w.CanTransitionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WorkflowTransitionResult(true));
            Service = new ActionService(Db, new RecordSecurityService(), Workflow.Object, Current.Object, Audit.Object, NullLogger<ActionService>.Instance);
            Finding = MakeFinding("OWN", Lead.Id, Other.Id);
            OtherFinding = MakeFinding("OTHER", Other.Id, Lead.Id);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            Overdue = MakeAction("OVERDUE", Finding, Party.Id, Auditee.Id, today.AddDays(-1), ActionStatus.Open);
            Today = MakeAction("TODAY", Finding, Party.Id, Auditee.Id, today, ActionStatus.Submitted);
            OtherAction = MakeAction("OTHER-A", OtherFinding, Other.Id, Auditee.Id, today.AddDays(1), ActionStatus.Open);
        }

        public async Task SeedAsync()
        {
            Db.Users.AddRange(Lead, Party, Auditee, Other);
            Db.Actions.AddRange(Overdue, Today, OtherAction);
            await Db.SaveChangesAsync();
        }

        public void As(User user, params string[] roles)
        {
            Current.SetupGet(u => u.UserId).Returns(user.EntraObjectId);
            Current.SetupGet(u => u.Roles).Returns(roles);
        }

        public CreateActionDto Create(Guid findingId) => new()
        {
            FindingId = findingId, ReferenceNumber = Guid.NewGuid().ToString(),
            ActionDescription = "Description", ActionRequired = "Required", ActionPartyId = Party.Id,
            AuditeeId = Auditee.Id, TargetDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)
        };

        public UpdateActionDto Update(ActionEntity action) => new()
        {
            ActionDescription = action.ActionDescription, ActionRequired = action.ActionRequired,
            ActionPartyId = action.ActionPartyId, AuditeeId = action.AuditeeId, TargetDate = action.TargetDate
        };

        public ValueTask DisposeAsync() => Db.DisposeAsync();

        private static User MakeUser(string name) => new()
        {
            Id = Guid.NewGuid(), EntraObjectId = Guid.NewGuid(), Email = $"{name}@example.invalid",
            DisplayName = name, Function = "F", Department = "D"
        };

        private static Finding MakeFinding(string reference, Guid lead, Guid coordinator) => new()
        {
            Id = Guid.NewGuid(), ReferenceNumber = reference, Description = "D", RootCause = "R", Recommendation = "R",
            Assessment = new Assessment
            {
                Id = Guid.NewGuid(), ReferenceNumber = reference, Name = reference, Scope = "S",
                Function = "F", Quarter = "Q1", Year = 2026, LeadAuditorId = lead, CoordinatorId = coordinator
            }
        };

        private static ActionEntity MakeAction(string reference, Finding finding, Guid party, Guid auditee, DateOnly target, ActionStatus status) => new()
        {
            Id = Guid.NewGuid(), Finding = finding, ReferenceNumber = reference, ActionDescription = "D",
            ActionRequired = "R", ActionPartyId = party, AuditeeId = auditee, TargetDate = target, Status = status
        };
    }
}
