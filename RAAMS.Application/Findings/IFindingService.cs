using RAAMS.Application.Common;

namespace RAAMS.Application.Findings;

public interface IFindingService
{
    Task<PaginatedResult<FindingDto>> ListAsync(FindingFilterDto filter, CancellationToken ct = default);
    Task<FindingDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<FindingDto> CreateAsync(CreateFindingDto dto, CancellationToken ct = default);
    Task<FindingDto> UpdateAsync(Guid id, UpdateFindingDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
