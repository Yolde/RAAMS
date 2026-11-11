using Microsoft.EntityFrameworkCore;
using Moq;
using RAAMS.API.Services;
using RAAMS.Application.Services;
using RAAMS.Domain.Entities.Actions;
using RAAMS.Domain.Entities.Assessments;
using RAAMS.Domain.Entities.Findings;
using RAAMS.Domain.Enums;
using RAAMS.Infrastructure.Data;

namespace RAAMS.Tests;

public class RecordSecurityServiceTests
{
    [Fact]
    public void ActionFiltersHonorEachRoleAndCombineMultipleRoles()
    {
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();
        var actions = new[]
        {
            CreateAction(ActionStatus.Closed, other, other, other),
            CreateAction(ActionStatus.Reopened, other, other, other),
            CreateAction(ActionStatus.Open, user, other, other),
            CreateAction(ActionStatus.Submitted, other, user, other),
            CreateAction(ActionStatus.Approved, other, other, user),
            CreateAction(ActionStatus.Open, other, other, other)
        }.AsQueryable();
        var service = new RecordSecurityService();

        Assert.Same(actions, service.ApplyActionFilter(actions, ["rEvIeWeR", "gra"], user));
        Assert.Same(actions, service.ApplyActionFilter(actions, ["superadmin"], user));
        Assert.Equal(2, service.ApplyActionFilter(actions, ["reviewer"], user).Count());
        Assert.Equal(2, service.ApplyActionFilter(actions, ["Sponsor"], user).Count());
        Assert.Equal(1, service.ApplyActionFilter(actions, ["ActionParty"], user).Count());
        Assert.Equal(1, service.ApplyActionFilter(actions, ["Auditee"], user).Count());
        Assert.Equal(1, service.ApplyActionFilter(actions, ["AssuranceCoordinator"], user).Count());
        Assert.Equal(1, service.ApplyActionFilter(actions, ["LeadAuditor"], user).Count());
        Assert.Equal(4, service.ApplyActionFilter(actions, ["Reviewer", "ActionParty", "Auditee"], user).Count());
        Assert.Empty(service.ApplyActionFilter(actions, ["Unknown"], user));
        Assert.Empty(service.ApplyActionFilter(actions, [], user));
    }

    [Fact]
    public void AssessmentFiltersHonorAssignmentsAndCombineRoles()
    {
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();
        var assessments = new[]
        {
            new Assessment { LeadAuditorId = user, CoordinatorId = other },
            new Assessment { LeadAuditorId = other, CoordinatorId = user },
            new Assessment { LeadAuditorId = other, CoordinatorId = other }
        }.AsQueryable();
        var service = new RecordSecurityService();

        Assert.Same(assessments, service.ApplyAssessmentFilter(assessments, ["GRA"], user));
        Assert.Same(assessments, service.ApplyAssessmentFilter(assessments, ["SuperAdmin"], user));
        Assert.Single(service.ApplyAssessmentFilter(assessments, ["leadauditor"], user));
        Assert.Single(service.ApplyAssessmentFilter(assessments, ["AssuranceCoordinator"], user));
        Assert.Equal(2, service.ApplyAssessmentFilter(assessments, ["LeadAuditor", "AssuranceCoordinator"], user).Count());
        Assert.Empty(service.ApplyAssessmentFilter(assessments, ["Auditee", "Reviewer"], user));
        Assert.Empty(service.ApplyAssessmentFilter(assessments, [], user));
    }

    [Fact]
    public void FiltersTranslateToSqlServerQueries()
    {
        var options = new DbContextOptionsBuilder<RaamsDbContext>()
            .UseSqlServer("Server=localhost;Database=RAAMS_ModelOnly;Integrated Security=true;TrustServerCertificate=true")
            .Options;
        using var db = new RaamsDbContext(options, Mock.Of<ICurrentUserService>());
        var service = new RecordSecurityService();
        var user = Guid.NewGuid();
        var actionSql = service.ApplyActionFilter(db.Actions, ["Reviewer", "LeadAuditor", "ActionParty"], user).ToQueryString();
        var assessmentSql = service.ApplyAssessmentFilter(db.Assessments, ["LeadAuditor", "AssuranceCoordinator"], user).ToQueryString();
        Assert.Contains("LeadAuditorId", actionSql);
        Assert.Contains("ActionPartyId", actionSql);
        Assert.Contains("Closed", actionSql);
        Assert.Contains("LeadAuditorId", assessmentSql);
        Assert.Contains("CoordinatorId", assessmentSql);
        Assert.Contains("WHERE", service.ApplyActionFilter(db.Actions, [], user).ToQueryString());
    }

    private static ActionEntity CreateAction(ActionStatus status, Guid actionPartyId, Guid auditeeId, Guid leadAuditorId) =>
        new()
        {
            Status = status,
            ActionPartyId = actionPartyId,
            AuditeeId = auditeeId,
            Finding = new Finding { Assessment = new Assessment { LeadAuditorId = leadAuditorId } }
        };
}
