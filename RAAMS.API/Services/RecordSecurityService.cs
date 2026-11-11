using RAAMS.Application.Authorization;
using RAAMS.Domain.Entities.Actions;
using RAAMS.Domain.Entities.Assessments;
using RAAMS.Domain.Enums;

namespace RAAMS.API.Services;

public class RecordSecurityService : IRecordSecurityService
{
    public IQueryable<ActionEntity> ApplyActionFilter(IQueryable<ActionEntity> query, IEnumerable<string> userRoles, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(userRoles);
        var roles = userRoles.ToArray();

        if (HasRole(roles, "GRA") || HasRole(roles, "SuperAdmin"))
            return query;

        var reviewerOrSponsor = HasRole(roles, "Reviewer") || HasRole(roles, "Sponsor");
        var actionParty = HasRole(roles, "ActionParty");
        var auditeeOrCoordinator = HasRole(roles, "Auditee") || HasRole(roles, "AssuranceCoordinator");
        var leadAuditor = HasRole(roles, "LeadAuditor");

        return query.Where(action =>
            (reviewerOrSponsor && (action.Status == ActionStatus.Closed || action.Status == ActionStatus.Reopened)) ||
            (actionParty && action.ActionPartyId == userId) ||
            (auditeeOrCoordinator && action.AuditeeId == userId) ||
            (leadAuditor && action.Finding.Assessment.LeadAuditorId == userId));
    }

    public IQueryable<Assessment> ApplyAssessmentFilter(IQueryable<Assessment> query, IEnumerable<string> userRoles, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(userRoles);
        var roles = userRoles.ToArray();

        if (HasRole(roles, "GRA") || HasRole(roles, "SuperAdmin"))
            return query;

        var leadAuditor = HasRole(roles, "LeadAuditor");
        var coordinator = HasRole(roles, "AssuranceCoordinator");
        return query.Where(assessment =>
            (leadAuditor && assessment.LeadAuditorId == userId) ||
            (coordinator && assessment.CoordinatorId == userId));
    }

    private static bool HasRole(IEnumerable<string> roles, string roleName) =>
        roles.Any(role => string.Equals(role, roleName, StringComparison.OrdinalIgnoreCase));
}
