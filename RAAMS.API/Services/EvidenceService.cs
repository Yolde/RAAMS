using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RAAMS.Application.Auditing;
using RAAMS.Application.Common;
using RAAMS.Application.Common.Exceptions;
using RAAMS.Application.Evidence;
using RAAMS.Application.Evidence.Validators;
using RAAMS.Application.Services;
using RAAMS.Domain.Entities.Evidence;
using RAAMS.Infrastructure.Data;

namespace RAAMS.API.Services;

public class EvidenceService(
    RaamsDbContext db,
    ICurrentUserService currentUser,
    IAuditContext auditContext,
    ILogger<EvidenceService> logger) : IEvidenceService
{
    private static readonly Expression<Func<EvidenceItem, EvidenceDto>> Projection = item => new EvidenceDto
    {
        Id = item.Id,
        ActionId = item.ActionId,
        AssessmentId = item.AssessmentId,
        FileName = item.FileName,
        ContentType = item.ContentType,
        SizeBytes = item.SizeBytes,
        Version = item.Version,
        UploadedByName = item.UploadedBy.DisplayName,
        UploadedAt = item.UploadedAt,
        SharePointUrl = item.SharePointUrl
    };

    private static bool HasRole(IEnumerable<string> roles, string role) =>
        roles.Any(value => string.Equals(value, role, StringComparison.OrdinalIgnoreCase));

    private async Task<Guid> ResolveUserIdAsync(CancellationToken ct)
    {
        if (currentUser.UserId is not Guid subject)
            throw new ForbiddenException("A registered user is required to modify evidence.");
        var id = await db.Users.AsNoTracking().Where(user => user.EntraObjectId == subject)
            .Select(user => user.Id).SingleOrDefaultAsync(ct);
        if (id == Guid.Empty)
            throw new ForbiddenException("A registered user is required to modify evidence.");
        return id;
    }

    // Keep active evidence visible when its uploader or action is soft-deleted.
    private IQueryable<EvidenceItem> Evidence() =>
        db.Evidence.IgnoreQueryFilters().AsNoTracking().Where(item => !item.IsDeleted);

    public async Task<PaginatedResult<EvidenceDto>> ListAsync(Guid? actionId, int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 200 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new ValidationException("Page must be positive and PageSize must be between 1 and 200.");
        var query = Evidence();
        if (actionId is Guid id)
            query = query.Where(item => item.ActionId == id);
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(item => item.UploadedAt).ThenBy(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(Projection).ToListAsync(ct);
        return new PaginatedResult<EvidenceDto>(items, count, page, pageSize);
    }

    public Task<EvidenceDto?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Evidence().Where(item => item.Id == id).Select(Projection).SingleOrDefaultAsync(ct);

    // ── NEW METHOD FOR FILE DOWNLOAD ──
    public async Task<EvidenceFile?> GetFileContentAsync(Guid id, CancellationToken ct = default)
    {
        var result = await (
            from e in db.Evidence.AsNoTracking()
            join c in db.EvidenceContents.AsNoTracking() on e.Id equals c.EvidenceId
            where e.Id == id && !e.IsDeleted
            select new EvidenceFile(e.FileName, e.ContentType, c.Data)
        ).SingleOrDefaultAsync(ct);

        return result;
    }
    // ──────────────────────────────────

    public async Task<EvidenceDto> CreateAsync(CreateEvidenceDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.ActionId is null && dto.AssessmentId is null)
            throw new ArgumentException("Evidence must reference either an Action or an Assessment");
        var roles = currentUser.Roles.ToArray();
        var gra = HasRole(roles, "GRA");
        if ((dto.AssessmentId.HasValue && !gra) ||
            (!gra && !HasRole(roles, "ActionParty") && !HasRole(roles, "Auditee")))
            throw new ForbiddenException("Only the assigned action party, assigned auditee or GRA can create evidence.");
        await new CreateEvidenceValidator().ValidateAndThrowAsync(dto, ct);
        var userId = await ResolveUserIdAsync(ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Lock the parent until commit so uploads through this service allocate versions serially.
        if (dto.ActionId is Guid actionId)
        {
            var action = await db.Actions.FromSqlInterpolated(
                    $"SELECT * FROM dbo.Actions WITH (UPDLOCK, HOLDLOCK) WHERE Id = {actionId} AND IsDeleted = 0")
                .IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(ct)
                ?? throw new ArgumentException("Action not found");
            if (!gra && !(HasRole(roles, "ActionParty") && action.ActionPartyId == userId) &&
                !(HasRole(roles, "Auditee") && action.AuditeeId == userId))
                throw new ForbiddenException("Only the assigned action party, assigned auditee or GRA can create evidence.");
        }
        if (dto.AssessmentId is Guid assessmentId)
        {
            var assessment = await db.Assessments.FromSqlInterpolated(
                    $"SELECT * FROM dbo.Assessments WITH (UPDLOCK, HOLDLOCK) WHERE Id = {assessmentId} AND IsDeleted = 0")
                .IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(ct)
                ?? throw new ArgumentException("Assessment not found");
        }
        var versions = db.Evidence.IgnoreQueryFilters();
        versions = dto.ActionId is Guid parentActionId
            ? versions.Where(item => item.ActionId == parentActionId)
            : versions.Where(item => item.ActionId == null && item.AssessmentId == dto.AssessmentId);
        var maxVersion = await versions.MaxAsync(item => (int?)item.Version, ct) ?? 0;
        var version = checked(maxVersion + 1);
        var item = new EvidenceItem
        {
            Id = Guid.NewGuid(), ActionId = dto.ActionId, AssessmentId = dto.AssessmentId,
            FileName = dto.FileName, ContentType = dto.ContentType,
            SizeBytes = dto.SizeBytes, Version = version, UploadedById = userId, UploadedAt = DateTime.UtcNow
        };
        db.Evidence.Add(item);
        auditContext.SetReason($"Evidence uploaded: {item.FileName} (v{version})");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("Created evidence {EvidenceId} for action {ActionId} and assessment {AssessmentId}",
            item.Id, dto.ActionId, dto.AssessmentId);
        return await GetByIdAsync(item.Id, ct)
            ?? throw new InvalidOperationException("The saved evidence could not be retrieved.");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var userId = await ResolveUserIdAsync(ct);
        var item = await db.Evidence.SingleOrDefaultAsync(evidence => evidence.Id == id && !evidence.IsDeleted, ct)
            ?? throw new ForbiddenException("Evidence not found or not visible to the current user.");
        if (!HasRole(currentUser.Roles, "GRA") && item.UploadedById != userId)
            throw new ForbiddenException("Only the uploader or GRA can delete evidence.");
        item.IsDeleted = true;
        auditContext.SetReason($"Evidence deleted: {item.FileName}");
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Deleted evidence {EvidenceId}", id);
    }
}