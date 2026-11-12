using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RAAMS.Application.Assessments;
using RAAMS.Domain.Entities.Roles;
using RAAMS.Infrastructure.Data;

namespace RAAMS.Tests.Integration;

public class RbacTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private HttpClient Client(Func<TestData, Guid> subject, string roles)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", subject(factory.Data).ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", roles);
        return client;
    }

    private CreateAssessmentDto Assessment() => new()
    {
        Name = "Integration assessment", Scope = "Integration scope", Function = "Assurance",
        Quarter = "Q2", Year = 2026, LeadAuditorId = factory.Data.HarunId,
        CoordinatorId = factory.Data.HarunId, ReferenceNumber = $"RA-TEST-{Guid.NewGuid():N}"[..20],
        LineOfDefence = "LOD1"
    };

    [Fact]
    public async Task GRA_CanCreateAssessment_Returns201()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", factory.Data.HarunId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", "GRA,SuperAdmin");
        var response = await client.PostAsJsonAsync("/api/assessments", Assessment());
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<AssessmentDto>();
        body.Should().NotBeNull();
        body!.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ActionParty_CannotCreateAssessment_Returns403()
    {
        using var client = Client(data => data.ApId, "ActionParty");
        var response = await client.PostAsJsonAsync("/api/assessments", Assessment());
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GRA_CanListAssessments_Returns200WithItems()
    {
        using var client = Client(data => data.HarunId, "GRA");
        var response = await client.GetAsync("/api/assessments");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await client.GetFromJsonAsync<JsonElement>("/api/assessments");
        body.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        body.GetProperty("items").GetArrayLength().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ActionParty_CannotListAssessments_Returns403()
    {
        using var client = Client(data => data.ApId, "ActionParty");
        var response = await client.GetAsync("/api/assessments");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ActionParty_WithAssessmentModuleAccess_GetsEmptyAssessmentsFromRecordFilter()
    {
        // A separate factory keeps this temporary module grant from affecting other tests.
        await using var scopedFactory = new CustomWebApplicationFactory();
        using var client = scopedFactory.CreateClient();
        using (var scope = scopedFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RaamsDbContext>();
            var roleId = await db.Roles.Where(role => role.Name == "ActionParty").Select(role => role.Id).SingleAsync();
            var permissionId = await db.Permissions.Where(permission => permission.Name == "Assessment.View")
                .Select(permission => permission.Id).SingleAsync();
            db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Add("X-Test-User-Id", scopedFactory.Data.ApId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", "ActionParty");
        var response = await client.GetAsync("/api/assessments");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await client.GetFromJsonAsync<JsonElement>("/api/assessments");
        body.GetProperty("totalCount").GetInt32().Should().Be(0);
        body.GetProperty("items").GetArrayLength().Should().Be(0);
    }
}
