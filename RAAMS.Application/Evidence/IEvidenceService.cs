using RAAMS.Application.Common;

namespace RAAMS.Application.Evidence;

public interface IEvidenceService
{
    Task<PaginatedResult<EvidenceDto>> ListAsync(Guid? actionId, int page, int pageSize, CancellationToken ct = default);
    Task<EvidenceDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<EvidenceDto> CreateAsync(CreateEvidenceDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
