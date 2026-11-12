using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RAAMS.Application.Auditing;
using RAAMS.Application.Authorization;
using RAAMS.Application.Common;
using RAAMS.Application.Common.Exceptions;
using RAAMS.Application.Findings;
using RAAMS.Application.Findings.Validators;
using RAAMS.Application.Services;
using RAAMS.Domain.Entities.Findings;
using RAAMS.Domain.Enums;
using RAAMS.Infrastructure.Data;

namespace RAAMS.API.Services;

public class FindingService(
    RaamsDbContext db,
    IRecordSecurityService recordSecurity,
    ICurrentUserService currentUser,
    IAuditContext auditContext,
    ILogger<FindingService> logger) : IFindingService
{
    private static readonly Expression<Func<Finding, FindingDto>> Projection = finding => new FindingDto
    {
        Id = finding.Id,
        AssessmentId = finding.AssessmentId,
        AssessmentName = finding.Assessment.Name,
        ReferenceNumber = finding.ReferenceNumber,
        Description = finding.Description,
        RootCause = finding.RootCause,
        Recommendation = finding.Recommendation,
        Severity = finding.Severity.ToString(),
        Status = finding.Status.ToString(),
        CreatedDate = finding.CreatedDate
    };

    private static bool HasRole(IEnumerable<string> roles, string role) =>
        roles.Any(value => string.Equals(value, role, StringComparison.OrdinalIgnoreCase));

    private async Task<Guid> ResolveUserIdAsync(CancellationToken ct)
    {
        if (currentUser.UserId is not Guid subject)
            return Guid.Empty;

        return await db.Users.AsNoTracking()
            .Where(user => user.EntraObjectId == subject)
            .Select(user => user.Id)
            .SingleOrDefaultAsync(ct);
    }

    private IQueryable<Guid> VisibleAssessmentIds(string[] roles, Guid userId)
    {
        var assessments = db.Assessments.Where(assessment => !assessment.IsDeleted);
        return recordSecurity.ApplyAssessmentFilter(assessments, roles, userId)
            .Select(assessment => assessment.Id);
    }

    private IQueryable<Finding> VisibleFindings(string[] roles, Guid userId, bool tracked = false)
    {
        var query = db.Findings.Where(finding => !finding.IsDeleted);
        if (!tracked)
            query = query.AsNoTracking();
        var ids = VisibleAssessmentIds(roles, userId);
        return query.Where(finding => ids.Contains(finding.AssessmentId));
    }

    private static void RequireWriter(string[] roles)
    {
        if (!HasRole(roles, "GRA") && !HasRole(roles, "LeadAuditor"))
            throw new ForbiddenException("Only GRA and lead auditors can modify findings.");
    }

    private IQueryable<Guid> WritableAssessmentIds(string[] roles, Guid userId)
    {
        // Other roles must not expand the lead auditor's write scope.
        var writerRoles = HasRole(roles, "GRA") ? new[] { "GRA" } : new[] { "LeadAuditor" };
        return VisibleAssessmentIds(writerRoles, userId);
    }

    public async Task<PaginatedResult<FindingDto>> ListAsync(FindingFilterDto filter, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (filter.Page < 1 || filter.PageSize is < 1 or > 200 ||
            (long)(filter.Page - 1) * filter.PageSize > int.MaxValue)
            throw new ValidationException("Page must be positive and PageSize must be between 1 and 200.");

        var roles = currentUser.Roles.ToArray();
        var userId = await ResolveUserIdAsync(ct);
        var query = VisibleFindings(roles, userId);
        if (filter.AssessmentId is Guid assessmentId)
            query = query.Where(finding => finding.AssessmentId == assessmentId);
        if (!string.IsNullOrWhiteSpace(filter.Severity))
        {
            if (!Enum.TryParse<FindingSeverity>(filter.Severity, false, out var severity) ||
                !Enum.IsDefined(severity) || severity.ToString() != filter.Severity)
                throw new ValidationException("Severity is not valid.");
            query = query.Where(finding => finding.Severity == severity);
        }
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            if (!Enum.TryParse<FindingStatus>(filter.Status, false, out var status) ||
                !Enum.IsDefined(status) || status.ToString() != filter.Status)
                throw new ValidationException("Status is not valid.");
            query = query.Where(finding => finding.Status == status);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(finding => finding.Severity == FindingSeverity.High ? 0 :
                                finding.Severity == FindingSeverity.Medium ? 1 : 2)
            .ThenByDescending(finding => finding.CreatedDate)
            .ThenBy(finding => finding.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(Projection)
            .ToListAsync(ct);
        return new PaginatedResult<FindingDto>(items, total, filter.Page, filter.PageSize);
    }

    public async Task<FindingDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var roles = currentUser.Roles.ToArray();
        var userId = await ResolveUserIdAsync(ct);
        return await VisibleFindings(roles, userId)
            .Where(finding => finding.Id == id)
            .Select(Projection)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<FindingDto> CreateAsync(CreateFindingDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var roles = currentUser.Roles.ToArray();
        RequireWriter(roles);
        await new CreateFindingValidator().ValidateAndThrowAsync(dto, ct);
        var userId = await ResolveUserIdAsync(ct);
        if (!await WritableAssessmentIds(roles, userId).AnyAsync(id => id == dto.AssessmentId, ct))
            throw new ForbiddenException("Assessment not found or not visible to the current user.");

        var finding = new Finding
        {
            Id = Guid.NewGuid(),
            AssessmentId = dto.AssessmentId,
            ReferenceNumber = dto.ReferenceNumber,
            Description = dto.Description,
            RootCause = dto.RootCause,
            Recommendation = dto.Recommendation,
            Severity = Enum.Parse<FindingSeverity>(dto.Severity),
            Status = FindingStatus.Draft
        };
        db.Findings.Add(finding);
        auditContext.SetReason($"Finding created: {finding.ReferenceNumber}");
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created finding {FindingId}", finding.Id);
        return await GetByIdAsync(finding.Id, ct)
            ?? throw new InvalidOperationException("The created finding is not visible to its creator.");
    }

    public async Task<FindingDto> UpdateAsync(Guid id, UpdateFindingDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var roles = currentUser.Roles.ToArray();
        RequireWriter(roles);
        await new UpdateFindingValidator().ValidateAndThrowAsync(dto, ct);
        var userId = await ResolveUserIdAsync(ct);
        var ids = WritableAssessmentIds(roles, userId);
        var finding = await db.Findings
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted && ids.Contains(item.AssessmentId), ct)
            ?? throw new ForbiddenException("Finding not found or not visible to the current user.");

        finding.Description = dto.Description;
        finding.RootCause = dto.RootCause;
        finding.Recommendation = dto.Recommendation;
        finding.Severity = Enum.Parse<FindingSeverity>(dto.Severity);
        auditContext.SetReason($"Finding updated: {finding.ReferenceNumber}");
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Updated finding {FindingId}", id);
        return await GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("The updated finding is not visible to its editor.");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var roles = currentUser.Roles.ToArray();
        if (!HasRole(roles, "GRA"))
            throw new ForbiddenException("Only GRA can delete findings.");

        var userId = await ResolveUserIdAsync(ct);
        var finding = await VisibleFindings(roles, userId, tracked: true)
            .SingleOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new ForbiddenException("Finding not found or not visible to the current user.");
        db.Findings.Remove(finding);
        auditContext.SetReason($"Finding deleted: {finding.ReferenceNumber}");
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Deleted finding {FindingId}", id);
    }
}
