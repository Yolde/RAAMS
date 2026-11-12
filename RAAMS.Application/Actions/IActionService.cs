using RAAMS.Application.Common;

namespace RAAMS.Application.Actions;

public interface IActionService
{
    Task<PaginatedResult<ActionDto>> ListAsync(ActionFilterDto filter, CancellationToken ct = default);
    Task<ActionDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ActionDto> CreateAsync(CreateActionDto dto, CancellationToken ct = default);
    Task<ActionDto> UpdateAsync(Guid id, UpdateActionDto dto, CancellationToken ct = default);
    Task<ActionDto> SubmitAsync(Guid id, SubmitActionDto dto, CancellationToken ct = default);
    Task<ActionDto> ApproveAsync(Guid id, ApproveActionDto dto, CancellationToken ct = default);
    Task<ActionDto> RejectAsync(Guid id, RejectActionDto dto, CancellationToken ct = default);
    Task<ActionDto> ReopenAsync(Guid id, RejectActionDto dto, CancellationToken ct = default);
}
