using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RAAMS.Domain.Enums;
using RAAMS.Infrastructure.Data;

namespace RAAMS.Tests.Integration;

public class EscalationEmailTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [SqlServerFact]
    public async Task Auditee_CanUploadEscalationEmail_Returns201()
    {
        await using var sqlFactory = new SqlServerWebApplicationFactory();
        using var client = sqlFactory.CreateClient();
        using (var scope = sqlFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RaamsDbContext>();
            var action = await db.Actions.SingleAsync(item => item.Id == sqlFactory.Data.ActionId);
            action.Status = ActionStatus.Closed;
            await db.SaveChangesAsync();
        }
        GovernanceTestHelpers.Authenticate(client, sqlFactory.Data.AuId, "Auditee");
        using var form = GovernanceTestHelpers.Email();
        var response = await client.PostAsync($"/api/assessments/{sqlFactory.Data.AssessmentId}/escalation-email", form);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var uploaded = await response.Content.ReadFromJsonAsync<JsonElement>();
        var emailId = uploaded.GetProperty("id").GetGuid();
        var list = await client.GetAsync($"/api/assessments/{sqlFactory.Data.AssessmentId}/escalation-emails");
        list.StatusCode.Should().Be(HttpStatusCode.OK, await list.Content.ReadAsStringAsync());
        var emails = await list.Content.ReadFromJsonAsync<JsonElement>();
        emails.GetArrayLength().Should().Be(1);
        emails[0].GetProperty("id").GetGuid().Should().Be(emailId);
        var download = await client.GetAsync($"/api/assessments/{sqlFactory.Data.AssessmentId}/escalation-emails/{emailId}/file");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        (await download.Content.ReadAsStringAsync()).Should().Contain("All actions closed.");
    }

    [Theory]
    [InlineData("GRA")]
    [InlineData("Auditee")]
    public async Task OpenAction_PreventsEscalationUpload(string role)
    {
        using var client = factory.CreateClient();
        GovernanceTestHelpers.Authenticate(client, role == "GRA" ? factory.Data.HarunId : factory.Data.AuId, role);
        using var form = GovernanceTestHelpers.Email();
        var response = await client.PostAsync($"/api/assessments/{factory.Data.AssessmentId}/escalation-email", form);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("action(s) must be closed first");
    }

    [Fact]
    public async Task ActionParty_CannotUploadEscalationEmail_Returns403()
    {
        using var client = factory.CreateClient();
        GovernanceTestHelpers.Authenticate(client, factory.Data.ApId, "ActionParty");
        using var form = GovernanceTestHelpers.Email();
        var response = await client.PostAsync($"/api/assessments/{factory.Data.AssessmentId}/escalation-email", form);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
