using RAAMS.Application.Common;

namespace RAAMS.Application.Reviews;

public interface IReviewService
{
    Task<PaginatedResult<ReviewDto>> ListAsync(Guid? actionId, int page, int pageSize, CancellationToken ct = default);
    Task<ReviewDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ReviewDto> CreateAsync(CreateReviewDto dto, CancellationToken ct = default);
}
