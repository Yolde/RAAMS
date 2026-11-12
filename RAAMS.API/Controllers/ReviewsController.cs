using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAAMS.API.Authorization;
using RAAMS.Application.Authorization;
using RAAMS.Application.Common;
using RAAMS.Application.Reviews;

namespace RAAMS.API.Controllers;

[ApiController]
[Route("api/reviews")]
[Authorize]
[RequireModule(Modules.Reviews)]
public class ReviewsController(IReviewService reviews, ILogger<ReviewsController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<ReviewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<ReviewDto>>> List(
        [FromQuery] Guid? actionId = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await reviews.ListAsync(actionId, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewDto>> GetById(Guid id, CancellationToken ct)
    {
        var review = await reviews.GetByIdAsync(id, ct);
        return review is null ? NotFound() : Ok(review);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ReviewDto>> Create([FromBody] CreateReviewDto dto, CancellationToken ct)
    {
        try
        {
            var review = await reviews.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = review.Id }, review);
        }
        catch (ArgumentException ex) when (ex.Message == "Action not found")
        {
            logger.LogInformation("Review creation failed: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }
}
