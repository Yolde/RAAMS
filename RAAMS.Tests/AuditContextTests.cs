using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using RAAMS.API.Services;

namespace RAAMS.Tests;

public class AuditContextTests
{
    [Fact]
    public async Task ValuesFlowAcrossAwaitAndOverridesTraceIdentifier()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-id" } };
        var context = new AuditContext(accessor);
        Assert.Null(context.Reason);
        Assert.True(Guid.TryParse(context.CorrelationId, out _));
        var fallback = context.CorrelationId;
        Assert.Equal(fallback, context.CorrelationId);

        context.SetReason("reason");
        var correlation = Guid.NewGuid().ToString();
        context.SetCorrelationId(correlation);
        await Task.Yield();
        Assert.Equal("reason", context.Reason);
        Assert.Equal(correlation, context.CorrelationId);
        Assert.Equal(("reason", correlation), await Task.Run(() => (context.Reason, context.CorrelationId)));
    }

    [Fact]
    public void SeparateInstancesAndMissingHttpContextAreIsolated()
    {
        var first = new AuditContext(null);
        var second = new AuditContext(null);
        first.SetReason("reason");
        first.SetCorrelationId(Guid.NewGuid().ToString());
        Assert.Null(second.Reason);
        Assert.Null(second.CorrelationId);
        Assert.Throws<ArgumentException>(() => first.SetCorrelationId("not-a-guid"));
    }

    [Fact]
    public async Task ParallelFlowsDoNotOverwriteParentOrEachOther()
    {
        var context = new AuditContext(null);
        context.SetReason("parent");
        async Task<string?> Child(string reason)
        {
            context.SetReason(reason);
            await Task.Yield();
            return context.Reason;
        }

        var values = await Task.WhenAll(Child("first"), Child("second"));
        Assert.Equal(new[] { "first", "second" }, values);
        Assert.Equal("parent", context.Reason);
    }

    [Fact]
    public void ReadsAuthenticatedSubjectAndEmailAndPreservesGuidTrace()
    {
        var subject = Guid.NewGuid();
        var trace = Guid.NewGuid().ToString();
        var http = new DefaultHttpContext { TraceIdentifier = trace };
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, subject.ToString()),
            new Claim(ClaimTypes.Email, "test@example.invalid")
        }, "test"));
        var context = new AuditContext(new HttpContextAccessor { HttpContext = http });
        Assert.Equal(subject, context.UserId);
        Assert.Equal("test@example.invalid", context.UserEmail);
        Assert.Equal(trace, context.CorrelationId);
        http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", subject.ToString()) }));
        Assert.Null(context.UserId);
    }
}
