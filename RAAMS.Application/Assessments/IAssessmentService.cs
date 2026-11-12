using RAAMS.Application.Common;

namespace RAAMS.Application.Assessments;

public interface IAssessmentService
{
    Task<PaginatedResult<AssessmentDto>> ListAsync(AssessmentFilterDto filter, CancellationToken ct = default);
    Task<AssessmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AssessmentDto> CreateAsync(CreateAssessmentDto dto, CancellationToken ct = default);
    Task<AssessmentDto> UpdateAsync(Guid id, UpdateAssessmentDto dto, CancellationToken ct = default);
    Task<AssessmentDto> ChangeStatusAsync(Guid id, ChangeAssessmentStatusDto dto, CancellationToken ct = default);
}
