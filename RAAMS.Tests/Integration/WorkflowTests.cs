using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RAAMS.Domain.Enums;

namespace RAAMS.Tests.Integration;

public class WorkflowTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Reviewer_CannotApproveAction_Returns403()
    {
        using var client = factory.CreateClient();
        var id = await GovernanceTestHelpers.AddActionAsync(factory.Services, factory.Data, ActionStatus.Submitted);
        GovernanceTestHelpers.Authenticate(client, factory.Data.ReviewerId, "Reviewer");
        var response = await client.PostAsJsonAsync($"/api/actions/{id}/approve", new { comments = "Approved" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("not visible");
        (await GovernanceTestHelpers.StatusAsync(factory.Services, id)).Should().Be(ActionStatus.Submitted);
    }

    [Fact]
    public async Task Auditee_CanApproveAction_Returns200()
    {
        using var client = factory.CreateClient();
        var id = await GovernanceTestHelpers.AddActionAsync(factory.Services, factory.Data, ActionStatus.Submitted);
        GovernanceTestHelpers.Authenticate(client, factory.Data.AuId, "Auditee");
        var response = await client.PostAsJsonAsync($"/api/actions/{id}/approve", new { comments = "Approved" });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await GovernanceTestHelpers.StatusAsync(factory.Services, id)).Should().Be(ActionStatus.Approved);
    }

    [Fact]
    public async Task ActionParty_CannotApproveOwnAction_Returns403()
    {
        using var client = factory.CreateClient();
        var id = await GovernanceTestHelpers.AddActionAsync(factory.Services, factory.Data, ActionStatus.Submitted);
        GovernanceTestHelpers.Authenticate(client, factory.Data.ApId, "ActionParty");
        var response = await client.PostAsJsonAsync($"/api/actions/{id}/approve", new { comments = "Approved" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("cannot perform");
        (await GovernanceTestHelpers.StatusAsync(factory.Services, id)).Should().Be(ActionStatus.Submitted);
    }

    [Fact]
    public async Task Reviewer_CanReopenClosedAction_Returns200()
    {
        using var client = factory.CreateClient();
        var id = await GovernanceTestHelpers.AddActionAsync(factory.Services, factory.Data, ActionStatus.Closed);
        GovernanceTestHelpers.Authenticate(client, factory.Data.ReviewerId, "Reviewer");
        var response = await client.PostAsJsonAsync($"/api/actions/{id}/reopen", new { comments = "Further work required" });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await GovernanceTestHelpers.StatusAsync(factory.Services, id)).Should().Be(ActionStatus.Reopened);
    }
}
