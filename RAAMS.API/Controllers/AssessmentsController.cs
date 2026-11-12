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

    [HttpPost("{id:guid}/escalation-email")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(21L * 1024 * 1024)]
    [ProducesResponseType(typeof(EscalationEmailDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<EscalationEmailDto>> UploadEscalationEmail(
        Guid id, [FromForm] UploadEscalationEmailRequest request, CancellationToken ct)
    {
        try
        {
            var file = request.File;
            var dto = new UploadEscalationEmailDto
            {
                FileName = Path.GetFileName(file.FileName.Replace('\\', '/')),
                ContentType = file.ContentType,
                SizeBytes = file.Length,
                Notes = request.Notes
            };
            await using var stream = file.OpenReadStream();
            var email = await assessments.UploadEscalationEmailAsync(id, dto, stream, ct);
            return CreatedAtAction(nameof(ListEscalationEmails), new { id }, email);
        }
        catch (ArgumentException ex) when (ex.Message == "Assessment not found")
        {
            logger.LogInformation("Escalation email upload failed: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/escalation-emails")]
    [ProducesResponseType(typeof(IReadOnlyList<EscalationEmailDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EscalationEmailDto>>> ListEscalationEmails(Guid id, CancellationToken ct) =>
        Ok(await assessments.ListEscalationEmailsAsync(id, ct));

    [HttpGet("{id:guid}/escalation-emails/{emailId:guid}/file")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadEscalationEmail(Guid id, Guid emailId, CancellationToken ct)
    {
        var file = await assessments.GetEscalationEmailFileAsync(id, emailId, ct);
        return file is null ? NotFound() : File(file.Data, file.ContentType, file.FileName);
    }
}
