using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RAAMS.API.Authorization;
using RAAMS.API.Controllers;
using RAAMS.Application.Evidence;

namespace RAAMS.Tests;

public class EvidenceControllerTests
{
    [Fact]
    public async Task CreateReturnsLocationAndMissingActionReturnsBadRequest()
    {
        var service = new Mock<IEvidenceService>();
        var input = new CreateEvidenceDto();
        var item = new EvidenceDto { Id = Guid.NewGuid() };
        service.Setup(s => s.CreateAsync(input, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        var controller = new EvidenceController(service.Object, NullLogger<EvidenceController>.Instance);
        var created = Assert.IsType<CreatedAtActionResult>((await controller.Create(input, default)).Result);
        Assert.Equal(nameof(EvidenceController.GetById), created.ActionName);
        Assert.Equal(item.Id, created.RouteValues!["id"]);
        service.Setup(s => s.CreateAsync(input, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Action not found"));
        Assert.IsType<BadRequestObjectResult>((await controller.Create(input, default)).Result);
        Assert.IsType<NoContentResult>(await controller.Delete(item.Id, default));
    }

    [Fact]
    public async Task GetReturnsNotFoundAndRequiresModule()
    {
        var service = new Mock<IEvidenceService>();
        service.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EvidenceDto?)null);
        var controller = new EvidenceController(service.Object, NullLogger<EvidenceController>.Instance);
        Assert.IsType<NotFoundResult>((await controller.GetById(Guid.NewGuid(), default)).Result);
        var attributes = typeof(EvidenceController).GetCustomAttributes(true);
        Assert.Contains(attributes, a => a is AuthorizeAttribute);
        Assert.Contains(attributes, a => a is RequireModuleAttribute module && module.Policy == "RequireModule:Evidence");
    }
}
