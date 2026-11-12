using RAAMS.Application.Common;

namespace RAAMS.Application.Assessments;

public interface IAssessmentService
{
    Task<PaginatedResult<AssessmentDto>> ListAsync(AssessmentFilterDto filter, CancellationToken ct = default);
    Task<AssessmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AssessmentDto> CreateAsync(CreateAssessmentDto dto, CancellationToken ct = default);
    Task<AssessmentDto> UpdateAsync(Guid id, UpdateAssessmentDto dto, CancellationToken ct = default);
    Task<AssessmentDto> ChangeStatusAsync(Guid id, ChangeAssessmentStatusDto dto, CancellationToken ct = default);
    Task<EscalationEmailDto> UploadEscalationEmailAsync(Guid assessmentId, UploadEscalationEmailDto dto, Stream content, CancellationToken ct = default);
    Task<IReadOnlyList<EscalationEmailDto>> ListEscalationEmailsAsync(Guid assessmentId, CancellationToken ct = default);
    Task<EscalationEmailFile?> GetEscalationEmailFileAsync(Guid assessmentId, Guid emailId, CancellationToken ct = default);
}
