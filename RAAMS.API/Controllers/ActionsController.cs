using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAAMS.API.Authorization;
using RAAMS.Application.Actions;
using RAAMS.Application.Authorization;
using RAAMS.Application.Authorization.Attributes;
using RAAMS.Application.Common;

namespace RAAMS.API.Controllers;

[ApiController]
[Route("api/actions")]
[Authorize]
[RequireModule(Modules.Actions)]
[Maskable("Action")]
public class ActionsController(IActionService actions) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<ActionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<ActionDto>>> List([FromQuery] ActionFilterDto filter, CancellationToken ct) =>
        Ok(await actions.ListAsync(filter, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ActionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActionDto>> GetById(Guid id, CancellationToken ct)
    {
        var action = await actions.GetByIdAsync(id, ct);
        return action is null ? NotFound() : Ok(action);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ActionDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ActionDto>> Create([FromBody] CreateActionDto dto, CancellationToken ct)
    {
        var action = await actions.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = action.Id }, action);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ActionDto>> Update(Guid id, [FromBody] UpdateActionDto dto, CancellationToken ct) =>
        Ok(await actions.UpdateAsync(id, dto, ct));

    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<ActionDto>> Submit(Guid id, [FromBody] SubmitActionDto dto, CancellationToken ct) =>
        Ok(await actions.SubmitAsync(id, dto, ct));

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ActionDto>> Approve(Guid id, [FromBody] ApproveActionDto dto, CancellationToken ct) =>
        Ok(await actions.ApproveAsync(id, dto, ct));

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<ActionDto>> Reject(Guid id, [FromBody] RejectActionDto dto, CancellationToken ct) =>
        Ok(await actions.RejectAsync(id, dto, ct));

    [HttpPost("{id:guid}/reopen")]
    public async Task<ActionResult<ActionDto>> Reopen(Guid id, [FromBody] RejectActionDto dto, CancellationToken ct) =>
        Ok(await actions.ReopenAsync(id, dto, ct));
}
