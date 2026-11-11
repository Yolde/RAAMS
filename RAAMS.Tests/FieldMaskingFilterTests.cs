using System.Security.Claims;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RAAMS.API.Filters;
using RAAMS.Application.Authorization;

namespace RAAMS.Tests;

public class FieldMaskingFilterTests
{
    [Maskable("Action")]
    private class MaskedController
    {
        public void Normal() { }
        [SkipFieldMasking] public void Skipped() { }
        [Maskable("User")] public void Override() { }
    }

    [SkipFieldMasking]
    [Maskable("Action")]
    private class SkippedController
    {
        public void Normal() { }
    }

    private class PlainController
    {
        public void Normal() { }
    }

    private class ExampleDto
    {
        [MaskableProperty("Secret")] public string? Secret { get; set; }
        [MaskableProperty("Number")] public int Number { get; set; }
        [MaskableProperty("Optional")] public int? Optional { get; set; }
        [MaskableProperty("Visible")] public string? Visible { get; set; }
        public string? Ordinary { get; set; }
    }

    private class ReadOnlyDto
    {
        [MaskableProperty("Secret")] public string Secret => "sensitive";
    }

    [Fact]
    public async Task MasksOnlyDeniedFieldsUsingRolesAndRequestCancellation()
    {
        var permission = new Mock<IFieldPermissionService>(MockBehavior.Strict);
        using var cancellation = new CancellationTokenSource();
        foreach (var field in new[] { "Secret", "Number", "Optional", "Visible" })
        {
            var allowed = field == "Visible";
            permission.Setup(p => p.GetAsync("Action", field,
                    It.Is<IEnumerable<string>>(r => r.SequenceEqual(new[] { "Reviewer" })), cancellation.Token))
                .ReturnsAsync(new FieldPermissionResult(allowed, false));
        }
        var dto = new ExampleDto { Secret = "secret", Number = 42, Optional = 7, Visible = "visible", Ordinary = "ordinary" };
        var result = new ObjectResult(dto);
        await RunAsync<MaskedController>(nameof(MaskedController.Normal), result, permission.Object, cancellation.Token);

        Assert.Null(dto.Secret);
        Assert.Equal(0, dto.Number);
        Assert.Null(dto.Optional);
        Assert.Equal("visible", dto.Visible);
        Assert.Equal("ordinary", dto.Ordinary);
        permission.VerifyAll();
    }

    [Fact]
    public async Task MaterializesLazyCollectionsAndCachesPermissionsPerField()
    {
        var permission = new Mock<IFieldPermissionService>();
        permission.Setup(p => p.GetAsync("Action", It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FieldPermissionResult(false, false));
        var enumerations = 0;
        IEnumerable<ExampleDto> Lazy()
        {
            enumerations++;
            yield return new ExampleDto { Secret = "first" };
            yield return new ExampleDto { Secret = "second" };
        }
        var result = new ObjectResult(Lazy()) { DeclaredType = typeof(IEnumerable<ExampleDto>) };
        await RunAsync<MaskedController>(nameof(MaskedController.Normal), result, permission.Object);

        Assert.Equal(1, enumerations);
        Assert.Null(result.DeclaredType);
        var values = Assert.IsType<List<object?>>(result.Value);
        Assert.All(values, value => Assert.Null(Assert.IsType<ExampleDto>(value).Secret));
        permission.Verify(p => p.GetAsync("Action", "Secret", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HonorsSkipAndActionOverride()
    {
        var permission = new Mock<IFieldPermissionService>(MockBehavior.Strict);
        var dto = new ExampleDto { Secret = "untouched" };
        await RunAsync<MaskedController>(nameof(MaskedController.Skipped), new ObjectResult(dto), permission.Object);
        await RunAsync<SkippedController>(nameof(SkippedController.Normal), new ObjectResult(dto), permission.Object);
        await RunAsync<PlainController>(nameof(PlainController.Normal), new ObjectResult(dto), permission.Object);
        Assert.Equal("untouched", dto.Secret);
        permission.Setup(p => p.GetAsync("User", It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FieldPermissionResult(false, false));
        await RunAsync<MaskedController>(nameof(MaskedController.Override), new ObjectResult(dto), permission.Object);
        Assert.Null(dto.Secret);
    }

    [Fact]
    public async Task RejectsUnwritableDeniedProperty()
    {
        var permission = new Mock<IFieldPermissionService>();
        permission.Setup(p => p.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FieldPermissionResult(false, false));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunAsync<MaskedController>(nameof(MaskedController.Normal), new ObjectResult(new ReadOnlyDto()), permission.Object));
    }

    private static async Task RunAsync<TController>(
        string method, IActionResult result, IFieldPermissionService permission, CancellationToken ct = default)
    {
        var services = new ServiceCollection().AddSingleton(permission).BuildServiceProvider();
        await using var disposableServices = services;
        var http = new DefaultHttpContext
        {
            RequestServices = services,
            RequestAborted = ct,
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("role", "Reviewer"), new Claim(ClaimTypes.Role, "reviewer")
            }, "test"))
        };
        var descriptor = new ControllerActionDescriptor
        {
            ControllerTypeInfo = typeof(TController).GetTypeInfo(),
            MethodInfo = typeof(TController).GetMethod(method)!
        };
        var actionContext = new ActionContext(http, new RouteData(), descriptor);
        var filters = new List<IFilterMetadata>();
        var executing = new ActionExecutingContext(actionContext, filters, new Dictionary<string, object?>(), new object());
        var executed = new ActionExecutedContext(actionContext, filters, new object()) { Result = result };
        await new FieldMaskingFilter(NullLogger<FieldMaskingFilter>.Instance)
            .OnActionExecutionAsync(executing, () => Task.FromResult(executed));
    }
}
