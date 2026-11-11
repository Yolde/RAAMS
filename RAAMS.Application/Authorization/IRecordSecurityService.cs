using RAAMS.Domain.Entities.Actions;
using RAAMS.Domain.Entities.Assessments;

namespace RAAMS.Application.Authorization;

public interface IRecordSecurityService
{
    IQueryable<ActionEntity> ApplyActionFilter(IQueryable<ActionEntity> query, IEnumerable<string> userRoles, Guid userId);
    IQueryable<Assessment> ApplyAssessmentFilter(IQueryable<Assessment> query, IEnumerable<string> userRoles, Guid userId);
}
