using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAAMS.API.Authorization;
using RAAMS.Application.Assessments;
using RAAMS.Application.Authorization;
using RAAMS.Application.Common;
using RAAMS.Application.Common.Exceptions;

namespace RAAMS.API.Controllers;

[ApiController]
[Route("api/assessments")]
[Authorize]
[RequireModule(Modules.Assessments)]
public class AssessmentsController(
    IAssessmentService assessments,
    ILogger<AssessmentsController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<AssessmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<AssessmentDto>>> List(
        [FromQuery] AssessmentFilterDto filter, CancellationToken ct)
    {
        try { return Ok(await assessments.ListAsync(filter, ct)); }
        catch (WorkflowViolationException ex) { logger.LogWarning(ex, "Assessment workflow rejected"); return BadRequest(new { error = ex.Message }); }
        catch (ForbiddenException ex) { logger.LogWarning(ex, "Assessment access denied"); return Forbid(); }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AssessmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssessmentDto>> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            var assessment = await assessments.GetByIdAsync(id, ct);
            return assessment is null ? NotFound() : Ok(assessment);
        }
        catch (WorkflowViolationException ex) { logger.LogWarning(ex, "Assessment workflow rejected"); return BadRequest(new { error = ex.Message }); }
        catch (ForbiddenException ex) { logger.LogWarning(ex, "Assessment access denied"); return Forbid(); }
    }

    [HttpPost]
    [ProducesResponseType(typeof(AssessmentDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<AssessmentDto>> Create([FromBody] CreateAssessmentDto dto, CancellationToken ct)
    {
        try
        {
            var assessment = await assessments.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = assessment.Id }, assessment);
        }
        catch (WorkflowViolationException ex) { logger.LogWarning(ex, "Assessment workflow rejected"); return BadRequest(new { error = ex.Message }); }
        catch (ForbiddenException ex) { logger.LogWarning(ex, "Assessment access denied"); return Forbid(); }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AssessmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AssessmentDto>> Update(Guid id, [FromBody] UpdateAssessmentDto dto, CancellationToken ct)
    {
        try { return Ok(await assessments.UpdateAsync(id, dto, ct)); }
        catch (WorkflowViolationException ex) { logger.LogWarning(ex, "Assessment workflow rejected"); return BadRequest(new { error = ex.Message }); }
        catch (ForbiddenException ex) { logger.LogWarning(ex, "Assessment access denied"); return Forbid(); }
    }

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(AssessmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AssessmentDto>> ChangeStatus(Guid id, [FromBody] ChangeAssessmentStatusDto dto, CancellationToken ct)
    {
        try { return Ok(await assessments.ChangeStatusAsync(id, dto, ct)); }
        catch (WorkflowViolationException ex) { logger.LogWarning(ex, "Assessment workflow rejected"); return BadRequest(new { error = ex.Message }); }
        catch (ForbiddenException ex) { logger.LogWarning(ex, "Assessment access denied"); return Forbid(); }
    }
}
