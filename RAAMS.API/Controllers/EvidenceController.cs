using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAAMS.API.Authorization;
using RAAMS.Application.Authorization;
using RAAMS.Application.Common;
using RAAMS.Application.Evidence;

namespace RAAMS.API.Controllers;

[ApiController]
[Route("api/evidence")]
[Authorize]
[RequireModule(Modules.Evidence)]
public class EvidenceController(IEvidenceService evidence, ILogger<EvidenceController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<EvidenceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<EvidenceDto>>> List(
        [FromQuery] Guid? actionId = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await evidence.ListAsync(actionId, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EvidenceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EvidenceDto>> GetById(Guid id, CancellationToken ct)
    {
        var item = await evidence.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [ProducesResponseType(typeof(EvidenceDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<EvidenceDto>> Create([FromBody] CreateEvidenceDto dto, CancellationToken ct)
    {
        try
        {
            var item = await evidence.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }
        catch (ArgumentException ex) when (ex.Message is "Action not found" or "Assessment not found" or
                                            "Evidence must reference either an Action or an Assessment")
        {
            logger.LogInformation("Evidence creation failed: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await evidence.DeleteAsync(id, ct);
        return NoContent();
    }
}
