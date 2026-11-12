using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RAAMS.API.Services;
using RAAMS.Application.Auditing;
using RAAMS.Application.Common.Exceptions;
using RAAMS.Application.Findings;
using RAAMS.Application.Services;
using RAAMS.Domain.Entities.Assessments;
using RAAMS.Domain.Entities.Findings;
using RAAMS.Domain.Entities.Users;
using RAAMS.Domain.Enums;
using RAAMS.Infrastructure.Auditing;
using RAAMS.Infrastructure.Data;

namespace RAAMS.Tests;

public class FindingServiceTests
{
    [Fact]
    public async Task ReadScopeAndSeverityOrderingFollowParentAssessments()
    {
        await using var fixture = new Fixture(["LeadAuditor"]);
        await fixture.SeedAsync();
        var page = await fixture.Service.ListAsync(new FindingFilterDto { PageSize = 2 });
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(new[] { "High", "Medium" }, page.Items.Select(item => item.Severity));
        Assert.All(page.Items, item => Assert.Equal("Owned", item.AssessmentName));
        var lastPage = await fixture.Service.ListAsync(new FindingFilterDto { PageSize = 2, Page = 2 });
        Assert.Equal("Low", Assert.Single(lastPage.Items).Severity);
        Assert.Null(await fixture.Service.GetByIdAsync(fixture.OtherFinding.Id));
        Assert.NotNull(await fixture.Service.GetByIdAsync(fixture.HighFinding.Id));

        var filtered = await fixture.Service.ListAsync(new FindingFilterDto
        {
            AssessmentId = fixture.Owned.Id, Severity = "High", Status = "Draft"
        });
        Assert.Equal(fixture.HighFinding.Id, Assert.Single(filtered.Items).Id);
        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Service.ListAsync(new FindingFilterDto { Severity = "99" }));
        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Service.ListAsync(new FindingFilterDto { Page = int.MaxValue, PageSize = 200 }));
    }

    [Fact]
    public async Task WriterMustBeLeadOfParentEvenWhenCoordinatorRoleBroadensReads()
    {
        await using var fixture = new Fixture(["LeadAuditor", "AssuranceCoordinator"]);
        await fixture.SeedAsync();
        Assert.NotNull(await fixture.Service.GetByIdAsync(fixture.OtherFinding.Id));
        var create = fixture.CreateDto(fixture.Other.Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Service.CreateAsync(create));
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Service.UpdateAsync(fixture.OtherFinding.Id, ValidUpdate()));
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Service.DeleteAsync(fixture.HighFinding.Id));

        var created = await fixture.Service.CreateAsync(fixture.CreateDto(fixture.Owned.Id));
        Assert.Equal("Draft", created.Status);
        Assert.Equal("Owned", created.AssessmentName);
        var updated = await fixture.Service.UpdateAsync(created.Id, ValidUpdate());
        Assert.Equal("Updated", updated.Description);
        fixture.Audit.Verify(a => a.SetReason("Finding created: NEW"), Times.Once);
        fixture.Audit.Verify(a => a.SetReason("Finding updated: NEW"), Times.Once);
    }

    [Fact]
    public async Task GraDeletesSoftlyAndDeletedParentsAreNeverVisible()
    {
        await using var fixture = new Fixture(["gRa"]);
        await fixture.SeedAsync();
        Assert.NotNull(await fixture.Service.GetByIdAsync(fixture.OtherFinding.Id));
        Assert.Null(await fixture.Service.GetByIdAsync(fixture.DeletedParentFinding.Id));
        await fixture.Service.DeleteAsync(fixture.OtherFinding.Id);
        Assert.Null(await fixture.Service.GetByIdAsync(fixture.OtherFinding.Id));
        Assert.True((await fixture.Db.Findings.IgnoreQueryFilters().SingleAsync(f => f.Id == fixture.OtherFinding.Id)).IsDeleted);
        fixture.Audit.Verify(a => a.SetReason($"Finding deleted: {fixture.OtherFinding.ReferenceNumber}"), Times.Once);
    }

    [Fact]
    public async Task UnknownRolesAndMismatchedSubjectsDenyAccessAndWrites()
    {
        await using var fixture = new Fixture(["LeadAuditor"]);
        await fixture.SeedAsync();
        fixture.Current.SetupGet(user => user.UserId).Returns(fixture.UserId);
        Assert.Empty((await fixture.Service.ListAsync(new FindingFilterDto())).Items);
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Service.CreateAsync(fixture.CreateDto(fixture.Owned.Id)));

        fixture.Current.SetupGet(user => user.UserId).Returns(fixture.Subject);
        fixture.Current.SetupGet(user => user.Roles).Returns(new[] { "SuperAdmin" });
        Assert.NotNull(await fixture.Service.GetByIdAsync(fixture.HighFinding.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Service.CreateAsync(fixture.CreateDto(fixture.Owned.Id)));
        await Assert.ThrowsAsync<ForbiddenException>(() => fixture.Service.DeleteAsync(fixture.HighFinding.Id));
    }

    private static UpdateFindingDto ValidUpdate() => new()
    {
        Description = "Updated", RootCause = "Cause", Recommendation = "Recommendation", Severity = "Medium"
    };

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid Subject { get; } = Guid.NewGuid();
        public Mock<ICurrentUserService> Current { get; } = new();
        public Mock<IAuditContext> Audit { get; } = new();
        public RaamsDbContext Db { get; }
        public FindingService Service { get; }
        public Assessment Owned { get; }
        public Assessment Other { get; }
        public Assessment DeletedParent { get; }
        public Finding HighFinding { get; }
        public Finding OtherFinding { get; }
        public Finding DeletedParentFinding { get; }

        public Fixture(string[] roles)
        {
            Current.SetupGet(user => user.UserId).Returns(Subject);
            Current.SetupGet(user => user.Roles).Returns(roles);
            var options = new DbContextOptionsBuilder<RaamsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString(), o => o.EnableNullChecks(false)).Options;
            Db = new RaamsDbContext(options, Current.Object, Audit.Object, NullLogger<AuditSaveChangesInterceptor>.Instance);
            Service = new FindingService(Db, new RecordSecurityService(), Current.Object, Audit.Object, NullLogger<FindingService>.Instance);
            var otherId = Guid.NewGuid();
            Owned = Assessment("Owned", UserId, otherId);
            Other = Assessment("Other", otherId, UserId);
            DeletedParent = Assessment("Deleted", UserId, otherId);
            DeletedParent.IsDeleted = true;
            HighFinding = Finding(Owned, "HIGH", FindingSeverity.High);
            OtherFinding = Finding(Other, "OTHER", FindingSeverity.High);
            DeletedParentFinding = Finding(DeletedParent, "DELETED", FindingSeverity.High);
        }

        public async Task SeedAsync()
        {
            Db.Users.Add(new User { Id = UserId, EntraObjectId = Subject, Email = "user@example.invalid",
                DisplayName = "User", Function = "F", Department = "D" });
            Db.Findings.AddRange(HighFinding, Finding(Owned, "MED", FindingSeverity.Medium),
                Finding(Owned, "LOW", FindingSeverity.Low), OtherFinding, DeletedParentFinding);
            await Db.SaveChangesAsync();
        }

        public CreateFindingDto CreateDto(Guid assessmentId) => new()
        {
            AssessmentId = assessmentId, ReferenceNumber = "NEW", Description = "Description",
            RootCause = "Cause", Recommendation = "Recommendation", Severity = "High"
        };

        public ValueTask DisposeAsync() => Db.DisposeAsync();

        private static Assessment Assessment(string name, Guid lead, Guid coordinator) => new()
        {
            Id = Guid.NewGuid(), Name = name, ReferenceNumber = name, Scope = "Scope", Function = "F",
            Quarter = "Q1", Year = 2026, LeadAuditorId = lead, CoordinatorId = coordinator
        };

        private static Finding Finding(Assessment parent, string reference, FindingSeverity severity) => new()
        {
            Id = Guid.NewGuid(), Assessment = parent, ReferenceNumber = reference, Description = "D",
            RootCause = "R", Recommendation = "R", Severity = severity, Status = FindingStatus.Draft
        };
    }
}
