using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using RAAMS.Application.Auth;

namespace RAAMS.Tests.Integration;

public class AuthTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Ping_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/auth/ping");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ping_WithValidToken_Returns200AndClaims()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", factory.Data.HarunId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", "GRA,SuperAdmin");
        var response = await client.GetAsync("/api/auth/ping");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await client.GetFromJsonAsync<JsonElement>("/api/auth/ping");
        body.GetProperty("status").GetString().Should().Be("authenticated");
        body.GetProperty("userId").GetGuid().Should().Be(factory.Data.HarunId);
    }

    [Fact]
    public async Task Me_WithGRA_ReturnsAllPermissions()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", factory.Data.HarunId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", "GRA,SuperAdmin");
        var response = await client.GetAsync("/api/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await client.GetFromJsonAsync<MeResponse>("/api/auth/me");
        body.Should().NotBeNull();
        body!.Roles.Should().Contain(["GRA", "SuperAdmin"]);
        body.Permissions.Should().Contain(["Assessment.View", "Action.Approve"]);
    }

    [Fact]
    public async Task Me_WithActionParty_ReturnsRestrictedPermissions()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", factory.Data.ApId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", "ActionParty");
        var response = await client.GetAsync("/api/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await client.GetFromJsonAsync<MeResponse>("/api/auth/me");
        body.Should().NotBeNull();
        body!.Roles.Should().Contain("ActionParty");
        body.Permissions.Should().Contain("Action.View").And.NotContain("Action.Approve");
    }
}
