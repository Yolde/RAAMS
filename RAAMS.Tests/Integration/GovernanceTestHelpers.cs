using System.Net.Http.Headers;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RAAMS.Domain.Entities.Actions;
using RAAMS.Domain.Enums;
using RAAMS.Infrastructure.Data;

namespace RAAMS.Tests.Integration;

internal static class GovernanceTestHelpers
{
    public static void Authenticate(HttpClient client, Guid userId, string role)
    {
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
    }

    public static async Task<Guid> AddActionAsync(IServiceProvider services, TestData data, ActionStatus status)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaamsDbContext>();
        var action = new ActionEntity
        {
            Id = Guid.NewGuid(), FindingId = data.FindingId, ReferenceNumber = $"TEST-{Guid.NewGuid():N}",
            ActionDescription = "Governance test", ActionRequired = "Resolve finding",
            ActionPartyId = data.ApId, AuditeeId = data.AuId,
            ActionTaken = "Corrective action completed", TargetDate = new DateOnly(2027, 1, 15),
            Status = status
        };
        db.Actions.Add(action);
        await db.SaveChangesAsync();
        return action.Id;
    }

    public static async Task<ActionStatus> StatusAsync(IServiceProvider services, Guid id)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<RaamsDbContext>().Actions.AsNoTracking()
            .Where(action => action.Id == id).Select(action => action.Status).SingleAsync();
    }

    public static MultipartFormDataContent Email()
    {
        var form = new MultipartFormDataContent();
        var file = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("Subject: Closed\r\n\r\nAll actions closed.")));
        file.Headers.ContentType = new MediaTypeHeaderValue("message/rfc822");
        form.Add(file, "file", "escalation.eml");
        form.Add(new StringContent("Closure confirmation"), "notes");
        return form;
    }
}
