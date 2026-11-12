using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RAAMS.API.Services;
using RAAMS.Application.Auditing;
using RAAMS.Application.Authorization;
using RAAMS.Application.Common.Exceptions;
using RAAMS.Application.Reviews;
using RAAMS.Application.Services;
using RAAMS.Domain.Entities.Actions;
using RAAMS.Domain.Entities.Assessments;
using RAAMS.Domain.Entities.Findings;
using RAAMS.Domain.Entities.Reviews;
using RAAMS.Domain.Entities.Users;
using RAAMS.Domain.Enums;
using RAAMS.Infrastructure.Auditing;
using RAAMS.Infrastructure.Data;

namespace RAAMS.Tests;

public class ReviewServiceTests
{
    [Fact]
    public async Task EffectiveReviewPersistsInternalReviewerAndLeavesActionClosed()
    {
        await using var f = new Fixture();
        await f.SeedAsync();
        var dto = await f.Service.CreateAsync(new CreateReviewDto { ActionId = f.Action.Id, Outcome = "Effective" });
        Assert.Equal(f.Reviewer.Id, dto.ReviewerId);
        Assert.Equal("Reviewer", dto.ReviewerName);
        Assert.Equal("ACTION", dto.ActionReference);
        Assert.Equal("Effective", dto.Outcome);
        Assert.Null(dto.ReasonCode);
        Assert.Equal("", dto.Comments);
        Assert.Equal(ActionStatus.Closed, f.Action.Status);
        Assert.Empty(await f.Db.ActionStatusHistories.ToListAsync());
        f.Workflow.Verify(w => w.CanTransitionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Audit.Verify(a => a.SetReason("Effectiveness review: Effective"), Times.Once);
    }

    [Fact]
    public async Task IneffectiveReviewReopensWithHistoryAndReason()
    {
        await using var f = new Fixture();
        await f.SeedAsync();
        var comments = new string('x', 500);
        var dto = await f.Service.CreateAsync(f.Ineffective(comments));
        Assert.Equal("Ineffective", dto.Outcome);
        Assert.Equal("MissingEvidence", dto.ReasonCode);
        Assert.Equal(comments, dto.Comments);
        Assert.Equal(ActionStatus.Reopened, f.Action.Status);
        var history = await f.Db.ActionStatusHistories.SingleAsync();
        Assert.Equal(f.Reviewer.Id, history.ActorId);
        Assert.Equal(ActionStatus.Closed, history.FromStatus);
        Assert.Equal(ActionStatus.Reopened, history.ToStatus);
        Assert.Equal(comments, history.Reason);
        Assert.Equal(dto.ReviewedAt, history.ChangedAt);
        f.Workflow.Verify(w => w.CanTransitionAsync("Action", "Closed", "Reopened",
            It.Is<IEnumerable<string>>(r => r.SequenceEqual(new[] { "Reviewer" })), It.IsAny<CancellationToken>()), Times.Once);
        f.Audit.Verify(a => a.SetReason("Effectiveness review: Ineffective — action reopened"), Times.Once);
    }

    [Fact]
    public async Task ListFiltersPagesAndRetainsHistoricalReferences()
    {
        await using var f = new Fixture();
        await f.SeedAsync();
        var old = new Review { Id = Guid.NewGuid(), ActionId = f.Action.Id, ReviewerId = f.Reviewer.Id,
            Outcome = ReviewOutcome.Effective, Comments = "old", ReviewedAt = DateTime.UtcNow.AddDays(-1) };
        var newer = new Review { Id = Guid.NewGuid(), ActionId = f.Action.Id, ReviewerId = f.Reviewer.Id,
            Outcome = ReviewOutcome.Ineffective, ReasonCode = ReviewReasonCode.Other, Comments = "new", ReviewedAt = DateTime.UtcNow };
        f.Db.Reviews.AddRange(old, newer);
        await f.Db.SaveChangesAsync();
        f.Action.IsDeleted = true;
        f.Reviewer.IsDeleted = true;
        await f.Db.SaveChangesAsync();
        var result = await f.Service.ListAsync(f.Action.Id, 1, 1);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(newer.Id, Assert.Single(result.Items).Id);
        Assert.Equal(old.Id, Assert.Single((await f.Service.ListAsync(null, 2, 1)).Items).Id);
        Assert.Empty((await f.Service.ListAsync(Guid.NewGuid(), 1, 20)).Items);
        Assert.Equal("Other", (await f.Service.GetByIdAsync(newer.Id))!.ReasonCode);
        Assert.Null(await f.Service.GetByIdAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<ValidationException>(() => f.Service.ListAsync(null, 0, 20));
        await Assert.ThrowsAsync<ValidationException>(() => f.Service.ListAsync(null, int.MaxValue, 200));
    }

    [Fact]
    public async Task InvalidInputDeniedWorkflowAndUnregisteredActorsDoNotCreateReviews()
    {
        await using var f = new Fixture();
        await f.SeedAsync();
        f.Current.SetupGet(u => u.Roles).Returns(new[] { "SuperAdmin" });
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.CreateAsync(f.Ineffective()));
        f.Current.SetupGet(u => u.Roles).Returns(new[] { "Reviewer" });
        f.Current.SetupGet(u => u.UserId).Returns(f.Reviewer.Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.CreateAsync(f.Ineffective()));
        f.Current.SetupGet(u => u.UserId).Returns(f.Reviewer.EntraObjectId);
        await Assert.ThrowsAsync<ValidationException>(() => f.Service.CreateAsync(f.Ineffective(new string('x', 501))));
        var missingReason = f.Ineffective();
        missingReason.ReasonCode = null;
        await Assert.ThrowsAsync<ValidationException>(() => f.Service.CreateAsync(missingReason));
        await Assert.ThrowsAsync<ValidationException>(() => f.Service.CreateAsync(new CreateReviewDto { ActionId = f.Action.Id, Outcome = "No" }));
        f.Workflow.Setup(w => w.CanTransitionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkflowTransitionResult(false, "Denied"));
        var denied = await Assert.ThrowsAsync<WorkflowViolationException>(() => f.Service.CreateAsync(f.Ineffective()));
        Assert.Equal("Denied", denied.Message);
        Assert.Equal(ActionStatus.Closed, f.Action.Status);
        Assert.Empty(await f.Db.Reviews.ToListAsync());
        Assert.Empty(await f.Db.ActionStatusHistories.ToListAsync());
    }

    [Fact]
    public async Task MissingDeletedOrNonClosedActionsCannotBeReviewed()
    {
        await using var f = new Fixture();
        await f.SeedAsync();
        var missing = f.Ineffective();
        missing.ActionId = Guid.NewGuid();
        Assert.Equal("Action not found", (await Assert.ThrowsAsync<ArgumentException>(() => f.Service.CreateAsync(missing))).Message);
        f.Action.Status = ActionStatus.Open;
        await f.Db.SaveChangesAsync();
        Assert.Equal("Can only review closed actions",
            (await Assert.ThrowsAsync<WorkflowViolationException>(() => f.Service.CreateAsync(f.Ineffective()))).Message);
        f.Action.Status = ActionStatus.Closed;
        f.Action.IsDeleted = true;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.CreateAsync(f.Ineffective()));
    }

    [Fact]
    public async Task GraCanReviewAndEffectiveCommentsCanBeTwoThousandCharacters()
    {
        await using var f = new Fixture();
        await f.SeedAsync();
        f.Current.SetupGet(u => u.Roles).Returns(new[] { "gRa" });
        var comments = new string('x', 2000);
        var dto = await f.Service.CreateAsync(new CreateReviewDto { ActionId = f.Action.Id, Outcome = "Effective", Comments = comments });
        Assert.Equal(comments, dto.Comments);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public User Reviewer { get; } = MakeUser("Reviewer");
        public User Other { get; } = MakeUser("Other");
        public Mock<ICurrentUserService> Current { get; } = new();
        public Mock<IAuditContext> Audit { get; } = new();
        public Mock<IWorkflowAuthorizationService> Workflow { get; } = new();
        public RaamsDbContext Db { get; }
        public ReviewService Service { get; }
        public ActionEntity Action { get; }

        public Fixture()
        {
            Current.SetupGet(u => u.UserId).Returns(Reviewer.EntraObjectId);
            Current.SetupGet(u => u.Roles).Returns(new[] { "Reviewer" });
            Workflow.Setup(w => w.CanTransitionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new WorkflowTransitionResult(true));
            var options = new DbContextOptionsBuilder<RaamsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString(), o => o.EnableNullChecks(false)).Options;
            Db = new RaamsDbContext(options, Current.Object, Audit.Object, NullLogger<AuditSaveChangesInterceptor>.Instance);
            Service = new ReviewService(Db, Workflow.Object, Current.Object, Audit.Object, NullLogger<ReviewService>.Instance);
            Action = new ActionEntity
            {
                Id = Guid.NewGuid(), ReferenceNumber = "ACTION", ActionDescription = "D", ActionRequired = "R",
                ActionPartyId = Other.Id, AuditeeId = Reviewer.Id, TargetDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Status = ActionStatus.Closed,
                Finding = new Finding
                {
                    Id = Guid.NewGuid(), ReferenceNumber = "F", Description = "D", RootCause = "R", Recommendation = "R",
                    Assessment = new Assessment
                    {
                        Id = Guid.NewGuid(), ReferenceNumber = "A", Name = "A", Scope = "S",
                        Function = "F", Quarter = "Q1", Year = 2026,
                        LeadAuditorId = Other.Id, CoordinatorId = Reviewer.Id
                    }
                }
            };
        }

        public async Task SeedAsync()
        {
            Db.Users.AddRange(Reviewer, Other);
            Db.Actions.Add(Action);
            await Db.SaveChangesAsync();
        }

        public CreateReviewDto Ineffective(string? comments = null) => new()
        {
            ActionId = Action.Id, Outcome = "Ineffective", ReasonCode = "MissingEvidence", Comments = comments
        };
        public ValueTask DisposeAsync() => Db.DisposeAsync();
        private static User MakeUser(string name) => new()
        {
            Id = Guid.NewGuid(), EntraObjectId = Guid.NewGuid(), DisplayName = name,
            Email = $"{name}@example.invalid", Function = "F", Department = "D"
        };
    }
}
