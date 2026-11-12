using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RAAMS.API.Authorization;
using RAAMS.API.Controllers;
using RAAMS.Application.Reviews;

namespace RAAMS.Tests;

public class ReviewsControllerTests
{
    [Fact]
    public async Task CreateReturnsLocationAndMissingActionReturnsBadRequest()
    {
        var service = new Mock<IReviewService>();
        var dto = new CreateReviewDto { ActionId = Guid.NewGuid(), Outcome = "Effective" };
        var review = new ReviewDto { Id = Guid.NewGuid() };
        service.Setup(s => s.CreateAsync(dto, It.IsAny<CancellationToken>())).ReturnsAsync(review);
        var controller = new ReviewsController(service.Object, NullLogger<ReviewsController>.Instance);
        var response = await controller.Create(dto, default);
        var created = Assert.IsType<CreatedAtActionResult>(response.Result);
        Assert.Equal(nameof(ReviewsController.GetById), created.ActionName);
        Assert.Equal(review.Id, created.RouteValues!["id"]);
        Assert.Same(review, created.Value);
        service.Setup(s => s.CreateAsync(dto, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Action not found"));
        Assert.IsType<BadRequestObjectResult>((await controller.Create(dto, default)).Result);
    }

    [Fact]
    public async Task GetReturnsNotFoundAndControllerRequiresReviewsModule()
    {
        var service = new Mock<IReviewService>();
        service.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReviewDto?)null);
        var controller = new ReviewsController(service.Object, NullLogger<ReviewsController>.Instance);
        Assert.IsType<NotFoundResult>((await controller.GetById(Guid.NewGuid(), default)).Result);
        var attributes = typeof(ReviewsController).GetCustomAttributes(true);
        Assert.Contains(attributes, a => a is AuthorizeAttribute);
        Assert.Contains(attributes, a => a is RequireModuleAttribute module && module.Policy == "RequireModule:Reviews");
    }
}
