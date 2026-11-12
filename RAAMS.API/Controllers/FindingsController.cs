using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAAMS.API.Authorization;
using RAAMS.Application.Authorization;
using RAAMS.Application.Common;
using RAAMS.Application.Findings;

namespace RAAMS.API.Controllers;

[ApiController]
[Route("api/findings")]
[Authorize]
[RequireModule(Modules.Findings)]
public class FindingsController(IFindingService findings) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<FindingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<FindingDto>>> List(
        [FromQuery] FindingFilterDto filter, CancellationToken ct) =>
        Ok(await findings.ListAsync(filter, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FindingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FindingDto>> GetById(Guid id, CancellationToken ct)
    {
        var finding = await findings.GetByIdAsync(id, ct);
        return finding is null ? NotFound() : Ok(finding);
    }

    [HttpPost]
    [ProducesResponseType(typeof(FindingDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<FindingDto>> Create([FromBody] CreateFindingDto dto, CancellationToken ct)
    {
        var finding = await findings.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = finding.Id }, finding);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(FindingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FindingDto>> Update(Guid id, [FromBody] UpdateFindingDto dto, CancellationToken ct) =>
        Ok(await findings.UpdateAsync(id, dto, ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await findings.DeleteAsync(id, ct);
        return NoContent();
    }
}
