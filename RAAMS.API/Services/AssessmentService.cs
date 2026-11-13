using FluentValidation;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Linq.Expressions;
using System.Security.Cryptography;
using RAAMS.Application.Actions;
using RAAMS.Application.Assessments;
using RAAMS.Application.Assessments.Validators;
using RAAMS.Application.Auditing;
using RAAMS.Application.Authorization;
using RAAMS.Application.Common;
using RAAMS.Application.Common.Exceptions;
using RAAMS.Application.Findings;
using RAAMS.Application.Services;
using RAAMS.Domain.Entities.Assessments;
using RAAMS.Domain.Entities.Evidence;
using RAAMS.Domain.Enums;
using RAAMS.Infrastructure.Data;

namespace RAAMS.API.Services;

public class AssessmentService(
    RaamsDbContext db,
    IRecordSecurityService recordSecurity,
    IWorkflowAuthorizationService workflowAuthorization,
    ICurrentUserService currentUser,
    IAuditContext auditContext,
    ILogger<AssessmentService> logger) : IAssessmentService
{
    private static readonly Expression<Func<Assessment, AssessmentDto>> AssessmentProjection = assessment => new()
    {
        Id = assessment.Id,
        ReferenceNumber = assessment.ReferenceNumber,
        Name = assessment.Name,
        Scope = assessment.Scope,
        Function = assessment.Function,
        Quarter = assessment.Quarter,
        Year = assessment.Year,
        LineOfDefence = assessment.LineOfDefence.ToString(),
        Opinion = assessment.Opinion.ToString(),
        LeadAuditorId = assessment.LeadAuditorId,
        LeadAuditorName = assessment.LeadAuditor.DisplayName,
        CoordinatorId = assessment.CoordinatorId,
        CoordinatorName = assessment.Coordinator.DisplayName,
        Status = assessment.Status.ToString(),
        CompletionDate = assessment.CompletionDate,
        CancellationReason = assessment.CancellationReason,
        CreatedDate = assessment.CreatedDate,
        ModifiedDate = assessment.ModifiedDate,
        
        Findings = assessment.Findings.Select(f => new FindingDto
        {
            Id = f.Id,
            AssessmentId = f.AssessmentId,
            ReferenceNumber = f.ReferenceNumber,
            Description = f.Description,
            RootCause = f.RootCause,
            Recommendation = f.Recommendation,
            Severity = f.Severity.ToString(),
            Status = f.Status.ToString(),
            CreatedDate = f.CreatedDate,
            Actions = f.Actions.Select(a => new ActionDto
            {
                Id = a.Id,
                FindingId = a.FindingId,
                FindingReference = f.ReferenceNumber,
                ReferenceNumber = a.ReferenceNumber,
                Reportable = a.Reportable.ToString(),
                ActionDescription = a.ActionDescription,
                ActionRequired = a.ActionRequired,
                ActionPartyId = a.ActionPartyId,
                ActionPartyName = a.ActionParty.DisplayName,
                AuditeeId = a.AuditeeId,
                AuditeeName = a.Auditee.DisplayName,
                TargetDate = a.TargetDate,
                ActionTaken = a.ActionTaken,
                Status = a.Status.ToString(),
                SubmittedDate = a.SubmittedDate,
                ApprovedDate = a.ApprovedDate,
                ClosedDate = a.ClosedDate,
                CreatedDate = a.CreatedDate
            }).ToList()
        }).ToList()
    };

    private static readonly Expression<Func<EvidenceItem, EscalationEmailDto>> EscalationProjection = item => new()
    {
        Id = item.Id,
        AssessmentId = item.AssessmentId!.Value,
        AssessmentReference = item.Assessment!.ReferenceNumber,
        FileName = item.FileName,
        ContentType = item.ContentType,
        SizeBytes = item.SizeBytes,
        UploadedByName = item.UploadedBy.DisplayName,
        UploadedAt = item.UploadedAt
    };

    private IQueryable<EvidenceItem> EscalationEmails(Guid assessmentId) =>
        db.Evidence.IgnoreQueryFilters().AsNoTracking()
            .Where(item => !item.IsDeleted && item.AssessmentId == assessmentId &&
                item.ActionId == null);

    public async Task<IReadOnlyList<EscalationEmailDto>> ListEscalationEmailsAsync(Guid assessmentId, CancellationToken ct = default) =>
        await EscalationEmails(assessmentId).OrderByDescending(item => item.UploadedAt)
            .ThenBy(item => item.Id).Select(EscalationProjection).ToListAsync(ct);

    public async Task<EscalationEmailFile?> GetEscalationEmailFileAsync(Guid assessmentId, Guid emailId, CancellationToken ct = default) =>
        await (from item in EscalationEmails(assessmentId)
               join content in db.EvidenceContents.AsNoTracking() on item.Id equals content.EvidenceId
               where item.Id == emailId
               select new EscalationEmailFile(item.FileName, item.ContentType, content.Data))
            .SingleOrDefaultAsync(ct);

    private async Task ValidateEscalationWorkflowAsync(Assessment assessment, CancellationToken ct)
    {
        if (assessment.Opinion is not (AssessmentOpinion.Unsatisfactory or AssessmentOpinion.Unacceptable) &&
            !await db.Findings.AnyAsync(finding => finding.AssessmentId == assessment.Id &&
                finding.Severity == FindingSeverity.High && !finding.IsDeleted, ct))
            throw new WorkflowViolationException("Escalation email only required for Unsatisfactory/Unacceptable or high-severity assessments");

        var openCount = await db.Actions.CountAsync(action => !action.IsDeleted &&
            action.Status != ActionStatus.Closed && db.Findings.Any(finding =>
                finding.Id == action.FindingId && finding.AssessmentId == assessment.Id), ct);
        if (openCount > 0)
            throw new WorkflowViolationException($"{openCount} action(s) must be closed first");
    }

    public async Task<EscalationEmailDto> UploadEscalationEmailAsync(
        Guid assessmentId, UploadEscalationEmailDto dto, Stream content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(content);
        if (currentUser.UserId is not Guid subject || subject == Guid.Empty)
            throw new ForbiddenException("Authentication required");
        if (!currentUser.Roles.Any(role => string.Equals(role, "Auditee", StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(role, "GRA", StringComparison.OrdinalIgnoreCase)))
            throw new ForbiddenException("Only Auditee or GRA can upload escalation emails");

        var userId = await db.Users.AsNoTracking().Where(user => user.EntraObjectId == subject)
            .Select(user => user.Id).SingleOrDefaultAsync(ct);
        if (userId == Guid.Empty)
            throw new ForbiddenException("A registered user is required to upload escalation emails.");
        var assessment = await db.Assessments.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == assessmentId && !item.IsDeleted, ct)
            ?? throw new ArgumentException("Assessment not found");
        await ValidateEscalationWorkflowAsync(assessment, ct);
        await new UploadEscalationEmailValidator().ValidateAndThrowAsync(dto, ct);
        const int maxSize = 20 * 1024 * 1024;
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk.AsMemory(), ct)) != 0)
        {
            if (buffer.Length + read > maxSize)
                throw new ValidationException("Email file must not exceed 20 MB.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        if (buffer.Length == 0 || buffer.Length != dto.SizeBytes)
            throw new ValidationException("Email file size does not match the uploaded content.");
        var data = buffer.ToArray();
        var reason = $"Escalation email uploaded for {assessment.ReferenceNumber}: {dto.FileName}";
        if (!string.IsNullOrWhiteSpace(dto.Notes))
            reason += $"; Notes: {dto.Notes}";
        if (reason.Length > 500)
            throw new ValidationException("The audit reason including notes must not exceed 500 characters.");

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        // Share the assessment lock and version sequence with assessment-only evidence uploads.
        assessment = await db.Assessments.FromSqlInterpolated(
                $"SELECT * FROM dbo.Assessments WITH (UPDLOCK, HOLDLOCK) WHERE Id = {assessmentId} AND IsDeleted = 0")
            .IgnoreQueryFilters().AsNoTracking()
            .OrderBy(a => a.Id)          // determinism: silence EF10103
            .FirstOrDefaultAsync(ct)
            ?? throw new ArgumentException("Assessment not found");
        await ValidateEscalationWorkflowAsync(assessment, ct);
        var maxVersion = await db.Evidence.IgnoreQueryFilters()
            .Where(item => item.AssessmentId == assessmentId && item.ActionId == null)
            .MaxAsync(item => (int?)item.Version, ct) ?? 0;
        var item = new EvidenceItem
        {
            Id = Guid.NewGuid(), AssessmentId = assessmentId, ActionId = null,
            FileName = dto.FileName, ContentType = dto.ContentType, SizeBytes = dto.SizeBytes,
            Version = checked(maxVersion + 1), UploadedById = userId, UploadedAt = DateTime.UtcNow,
            FileHash = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant()
        };
        db.Evidence.Add(item);
        db.EvidenceContents.Add(new EvidenceContent { EvidenceId = item.Id, Data = data });
        auditContext.SetReason(reason);
        await db.SaveChangesAsync(ct);
        var result = await EscalationEmails(assessmentId).Where(email => email.Id == item.Id)
            .Select(EscalationProjection).SingleAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("Created escalation email {EvidenceId} for assessment {AssessmentId}", item.Id, assessmentId);
        return result;
    }

    private async Task<Guid> ResolveCurrentUserIdAsync(CancellationToken ct)
    {
        if (currentUser.UserId is not Guid subject)
            return Guid.Empty;

        return await db.Users.AsNoTracking()
            .Where(user => user.Id == subject || user.EntraObjectId == subject)
            .OrderBy(user => user.Id == subject ? 0 : 1)
            .Select(user => user.Id)
            .FirstOrDefaultAsync(ct);
    }

    private IQueryable<Assessment> VisibleAssessments(Guid userId) =>
        recordSecurity.ApplyAssessmentFilter(
            db.Assessments.AsNoTracking().Where(assessment => !assessment.IsDeleted),
            currentUser.Roles, userId);

    private IQueryable<Assessment> EditableAssessments(Guid userId) =>
        recordSecurity.ApplyAssessmentFilter(
            db.Assessments.Where(assessment => !assessment.IsDeleted),
            currentUser.Roles, userId);

    public async Task<PaginatedResult<AssessmentDto>> ListAsync(AssessmentFilterDto filter, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (filter.Page < 1 || filter.PageSize is < 1 or > 200)
            throw new ValidationException("Page must be positive and PageSize must be between 1 and 200.");

        var userId = await ResolveCurrentUserIdAsync(ct);
        var query = VisibleAssessments(userId);
        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(assessment => assessment.Status.ToString() == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.Function))
            query = query.Where(assessment => assessment.Function == filter.Function);
        if (!string.IsNullOrWhiteSpace(filter.Quarter))
            query = query.Where(assessment => assessment.Quarter == filter.Quarter);
        if (filter.Year is int year)
            query = query.Where(assessment => assessment.Year == year);
        if (filter.LeadAuditorId is Guid leadAuditorId)
            query = query.Where(assessment => assessment.LeadAuditorId == leadAuditorId);

        var totalCount = await query.CountAsync(ct);
        var items = await query.OrderByDescending(assessment => assessment.CreatedDate)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(AssessmentProjection)
            .ToListAsync(ct);

        return new PaginatedResult<AssessmentDto>(items, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<AssessmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var userId = await ResolveCurrentUserIdAsync(ct);
        return await VisibleAssessments(userId)
            .Where(assessment => assessment.Id == id)
            .Select(AssessmentProjection)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<AssessmentDto> CreateAsync(CreateAssessmentDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await new CreateAssessmentValidator().ValidateAndThrowAsync(dto, ct);

        var sequence = await db.Assessments.IgnoreQueryFilters()
            .CountAsync(assessment => assessment.Year == dto.Year && assessment.Quarter == dto.Quarter, ct) + 1;
        var now = DateTime.UtcNow;
        var entity = new Assessment
        {
            Id = Guid.NewGuid(),
            ReferenceNumber = $"RA-{dto.Year}-{dto.Quarter}-{sequence:0000}",
            Name = dto.Name,
            Scope = dto.Scope,
            Function = dto.Function,
            Quarter = dto.Quarter,
            Year = dto.Year,
            LineOfDefence = Enum.Parse<LineOfDefence>(dto.LineOfDefence),
            Opinion = dto.Opinion is null ? AssessmentOpinion.NotRated : Enum.Parse<AssessmentOpinion>(dto.Opinion),
            LeadAuditorId = dto.LeadAuditorId,
            CoordinatorId = dto.CoordinatorId,
            Status = AssessmentStatus.Planned,
            CreatedDate = now,
            ModifiedDate = now,
            IsDeleted = false
        };
        db.Assessments.Add(entity);
        auditContext.SetReason($"Assessment created: {dto.Name}");
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created assessment {AssessmentId} with reference {ReferenceNumber}",
            entity.Id, entity.ReferenceNumber);

        return await GetByIdAsync(entity.Id, ct)
               ?? throw new InvalidOperationException("The created assessment is not visible to its creator.");
    }

    public async Task<AssessmentDto> UpdateAsync(Guid id, UpdateAssessmentDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await new UpdateAssessmentValidator().ValidateAndThrowAsync(dto, ct);
        var userId = await ResolveCurrentUserIdAsync(ct);
        var entity = await EditableAssessments(userId).SingleOrDefaultAsync(assessment => assessment.Id == id, ct)
                     ?? throw new ForbiddenException("Assessment not found or not visible to the current user.");

        entity.Name = dto.Name;
        entity.Scope = dto.Scope;
        entity.Function = dto.Function;
        entity.Quarter = dto.Quarter;
        entity.Year = dto.Year;
        entity.LineOfDefence = Enum.Parse<LineOfDefence>(dto.LineOfDefence);
        if (dto.Opinion is not null)
            entity.Opinion = Enum.Parse<AssessmentOpinion>(dto.Opinion);
        entity.LeadAuditorId = dto.LeadAuditorId;
        entity.CoordinatorId = dto.CoordinatorId;
        auditContext.SetReason($"Assessment updated: {dto.Name}");
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Updated assessment {AssessmentId}", id);

        return await GetByIdAsync(id, ct)
               ?? throw new InvalidOperationException("The updated assessment is not visible to its editor.");
    }

    public async Task<AssessmentDto> ChangeStatusAsync(Guid id, ChangeAssessmentStatusDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await new ChangeAssessmentStatusValidator().ValidateAndThrowAsync(dto, ct);
        var userId = await ResolveCurrentUserIdAsync(ct);
        var entity = await EditableAssessments(userId).SingleOrDefaultAsync(assessment => assessment.Id == id, ct)
                     ?? throw new ForbiddenException("Assessment not found or not visible to the current user.");
        var currentStatus = entity.Status.ToString();
        var result = await workflowAuthorization.CanTransitionAsync(
            "Assessment", currentStatus, dto.TargetStatus, currentUser.Roles, ct);
        if (!result.Allowed)
            throw new WorkflowViolationException(result.Reason ?? "Transition not permitted");

        if (!Enum.TryParse<AssessmentStatus>(dto.TargetStatus, ignoreCase: false, out var targetStatus) ||
            !Enum.IsDefined(targetStatus))
            throw new ValidationException("TargetStatus is not a valid assessment status.");

        if (targetStatus == AssessmentStatus.Completed)
            entity.CompletionDate = DateTime.UtcNow;
        if (targetStatus == AssessmentStatus.Cancelled)
            entity.CancellationReason = dto.Reason;
        entity.Status = targetStatus;
        auditContext.SetReason($"Assessment status: {currentStatus} -> {dto.TargetStatus}. {dto.Reason ?? string.Empty}");
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Changed assessment {AssessmentId} status from {FromStatus} to {ToStatus}",
            id, currentStatus, dto.TargetStatus);

        return await GetByIdAsync(id, ct)
               ?? throw new InvalidOperationException("The changed assessment is not visible to its editor.");
    }
}