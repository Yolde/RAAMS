using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RAAMS.Domain.Enums;

namespace RAAMS.Tests.Integration;

public class FieldMaskingTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task GRA_SeesTargetDateInActionResponse()
    {
        using var client = factory.CreateClient();
        var id = await GovernanceTestHelpers.AddActionAsync(factory.Services, factory.Data, ActionStatus.Open);
        GovernanceTestHelpers.Authenticate(client, factory.Data.HarunId, "GRA");
        var response = await client.GetAsync($"/api/actions/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("targetDate").GetString().Should().Be("2027-01-15");
    }

    [Fact]
    public async Task ActionParty_GetsNullTargetDateAndSeesActionTaken()
    {
        using var client = factory.CreateClient();
        var id = await GovernanceTestHelpers.AddActionAsync(factory.Services, factory.Data, ActionStatus.Open);
        GovernanceTestHelpers.Authenticate(client, factory.Data.ApId, "ActionParty");
        var response = await client.GetAsync($"/api/actions/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("targetDate").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("actionTaken").GetString().Should().Be("Corrective action completed");
    }

    [Fact]
    public async Task ActionParty_SeesStatusUnderCurrentRbacSeed()
    {
        using var client = factory.CreateClient();
        var id = await GovernanceTestHelpers.AddActionAsync(factory.Services, factory.Data, ActionStatus.Open);
        GovernanceTestHelpers.Authenticate(client, factory.Data.ApId, "ActionParty");
        var response = await client.GetAsync($"/api/actions/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("Open");
    }
}
