using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RAAMS.API.Services;
using RAAMS.Application.Assessments;
using RAAMS.Application.Auditing;
using RAAMS.Application.Authorization;
using RAAMS.Application.Services;
using RAAMS.Domain.Entities.Assessments;
using RAAMS.Domain.Entities.Users;
using RAAMS.Domain.Enums;
using RAAMS.Infrastructure.Auditing;
using RAAMS.Infrastructure.Data;

namespace RAAMS.Tests;

public class AssessmentServiceTests
{
    [Fact]
    public async Task ListUsesRecordScopeFiltersAndPagination()
    {
        var userId = Guid.NewGuid();
        var options = NewOptions();
        await using (var db = NewDb(options, Mock.Of<ICurrentUserService>()))
        {
            var otherId = Guid.NewGuid();
            db.Users.AddRange(
                User(userId, Guid.NewGuid(), "Lead"),
                User(otherId, Guid.NewGuid(), "Other"));
            db.Assessments.AddRange(
                Assessment("RA-2026-Q1-0001", userId, "A"),
                Assessment("RA-2026-Q1-0002", userId, "B"),
                Assessment("RA-2026-Q1-0003", otherId, "C"));
            await db.SaveChangesAsync();
        }

        await using var queryDb = NewDb(options, Current(userId, ["LeadAuditor"]));
        var service = Service(queryDb, Current(userId, ["LeadAuditor"]));
        var page = await service.ListAsync(new AssessmentFilterDto { Page = 1, PageSize = 1, Quarter = "Q1" });

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Single(page.Items);
        Assert.Equal("Lead", page.Items[0].LeadAuditorName);
    }

    [Fact]
    public async Task CreateGeneratesReferenceAndUpdateAndGetUseScope()
    {
        var userId = Guid.NewGuid();
        var entraId = Guid.NewGuid();
        var current = Current(userId, ["GRA"]);
        var options = NewOptions();
        await using var db = NewDb(options, current);
        db.Users.Add(User(userId, entraId, "Admin"));
        await db.SaveChangesAsync();
        var service = Service(db, current);
        var dto = new CreateAssessmentDto
        {
            Name = "First", Scope = "Scope", Function = "Finance", Quarter = "Q2", Year = 2026,
            LeadAuditorId = userId, CoordinatorId = userId, LineOfDefence = "LOD2", Opinion = "Satisfactory"
        };
        var created = await service.CreateAsync(dto);
        Assert.Equal("RA-2026-Q2-0001", created.ReferenceNumber);
        Assert.Equal("LOD2", created.LineOfDefence);
        Assert.Equal("Satisfactory", created.Opinion);
        Assert.Equal(nameof(AssessmentStatus.Planned), created.Status);
        Assert.Equal(userId, created.LeadAuditorId);
        Assert.NotEqual(Guid.Empty, created.Id);

        var updated = await service.UpdateAsync(created.Id, new UpdateAssessmentDto
        {
            Name = "Updated", Scope = "New scope", Function = "Finance", Quarter = "Q2", Year = 2026,
            LeadAuditorId = userId, CoordinatorId = userId, ReferenceNumber = "ignored", LineOfDefence = "LOD3"
        });
        Assert.Equal("Updated", updated.Name);
        Assert.Equal("LOD3", updated.LineOfDefence);
        Assert.Equal("Satisfactory", updated.Opinion);
        var defaultOpinion = await service.CreateAsync(new CreateAssessmentDto
        {
            Name = "Default", Scope = "Scope", Function = "Finance", Quarter = "Q2", Year = 2026,
            LeadAuditorId = userId, CoordinatorId = userId, LineOfDefence = "LOD1"
        });
        Assert.Equal("NotRated", defaultOpinion.Opinion);
        Assert.NotNull(await service.GetByIdAsync(created.Id));
    }

    [Fact]
    public async Task StatusChangeRequiresWorkflowAuthorizationAndSetsCompletion()
    {
        var userId = Guid.NewGuid();
        var current = Current(userId, ["LeadAuditor"]);
        var options = NewOptions();
        await using var db = NewDb(options, current);
        db.Users.Add(User(userId, Guid.NewGuid(), "Lead"));
        var assessment = Assessment("RA-2026-Q1-0001", userId, "Test");
        db.Assessments.Add(assessment);
        await db.SaveChangesAsync();

        var deniedWorkflow = new Mock<IWorkflowAuthorizationService>();
        deniedWorkflow.Setup(s => s.CanTransitionAsync("Assessment", "Planned", "Started",
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkflowTransitionResult(false, "not allowed"));
        var deniedService = Service(db, current, deniedWorkflow.Object);
        await Assert.ThrowsAsync<RAAMS.Application.Common.Exceptions.WorkflowViolationException>(() =>
            deniedService.ChangeStatusAsync(assessment.Id, new ChangeAssessmentStatusDto { TargetStatus = "Started" }));

        var allowedWorkflow = new Mock<IWorkflowAuthorizationService>();
        allowedWorkflow.Setup(s => s.CanTransitionAsync("Assessment", "Planned", "Completed",
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkflowTransitionResult(true));
        var allowedService = Service(db, current, allowedWorkflow.Object);
        var completed = await allowedService.ChangeStatusAsync(
            assessment.Id, new ChangeAssessmentStatusDto { TargetStatus = "Completed" });
        Assert.Equal("Completed", completed.Status);
        Assert.NotNull(completed.CompletionDate);
    }

    private static AssessmentService Service(
        RaamsDbContext db, ICurrentUserService current,
        IWorkflowAuthorizationService? workflow = null)
    {
        var audit = Mock.Of<IAuditContext>();
        var workflowService = workflow ?? Mock.Of<IWorkflowAuthorizationService>();
        return new AssessmentService(db, new RecordSecurityService(), workflowService, current,
            audit, NullLogger<AssessmentService>.Instance);
    }

    private static ICurrentUserService Current(Guid id, IEnumerable<string> roles)
    {
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(service => service.UserId).Returns(id);
        current.SetupGet(service => service.Roles).Returns(roles);
        return current.Object;
    }

    private static User User(Guid id, Guid entraId, string name) => new()
    {
        Id = id, EntraObjectId = entraId, DisplayName = name, Email = $"{name}@example.invalid",
        Function = "Finance", Department = "Finance"
    };

    private static Assessment Assessment(string reference, Guid leadId, string name) => new()
    {
        Id = Guid.NewGuid(), ReferenceNumber = reference, Name = name, Scope = "Scope",
        Function = "Finance", Quarter = "Q1", Year = 2026, LeadAuditorId = leadId,
        CoordinatorId = leadId, Status = AssessmentStatus.Planned
    };

    private static DbContextOptions<RaamsDbContext> NewOptions() =>
        new DbContextOptionsBuilder<RaamsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), builder => builder.EnableNullChecks(false))
            .Options;

    private static RaamsDbContext NewDb(DbContextOptions<RaamsDbContext> options, ICurrentUserService current) =>
        new(options, current, Mock.Of<IAuditContext>(), NullLogger<AuditSaveChangesInterceptor>.Instance);

}
