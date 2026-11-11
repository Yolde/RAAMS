using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using RAAMS.API.Authorization;
using RAAMS.Application.Authorization;
using RAAMS.Application.Auditing;
using RAAMS.Application.Services;
using RAAMS.Domain.Entities.Roles;
using RAAMS.Infrastructure.Data;

namespace RAAMS.Tests;

public class ModuleAuthorizationTests
{
    [Fact]
    public async Task DynamicPoliciesPreserveExistingNamedAndFallbackPolicies()
    {
        var options = new AuthorizationOptions();
        options.AddPolicy("RequireReviewer", policy => policy.RequireRole("Reviewer"));
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireClaim("fallback").Build();
        var provider = new ModulePolicyProvider(Options.Create(options));

        var modulePolicy = await provider.GetPolicyAsync("RequireModule:Actions");
        Assert.NotNull(modulePolicy);
        Assert.Contains(modulePolicy.Requirements, r => r is RequireModuleRequirement { Module: "Actions" });
        Assert.Contains(modulePolicy.Requirements, r => r is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);
        var named = await provider.GetPolicyAsync("RequireReviewer");
        Assert.NotNull(named);
        Assert.Contains(named.Requirements, r => r is Microsoft.AspNetCore.Authorization.Infrastructure.RolesAuthorizationRequirement);
        Assert.Null(await provider.GetPolicyAsync("Unknown"));
        Assert.Same(options.FallbackPolicy, await provider.GetFallbackPolicyAsync());
        Assert.Throws<InvalidOperationException>(() => { _ = provider.GetPolicyAsync("RequireModule:"); });
    }

    [Fact]
    public async Task HandlerUsesModuleColumnAndCaseInsensitiveRoles()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<ICurrentUserService>());
        services.AddSingleton(Mock.Of<IAuditContext>());
        services.AddLogging();
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<RaamsDbContext>(o => o.UseInMemoryDatabase(databaseName));
        await using var container = services.BuildServiceProvider();
        await using (var scope = container.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RaamsDbContext>();
            var role = new Role { Id = Guid.NewGuid(), Name = "ActionParty" };
            var permission = new Permission { Id = Guid.NewGuid(), Name = "Action.View", Module = Modules.Actions };
            db.RolePermissions.Add(new RolePermission { Role = role, Permission = permission });
            await db.SaveChangesAsync();
        }

        var handler = new RequireModuleHandler(container.GetRequiredService<IServiceScopeFactory>());
        async Task<bool> IsAllowed(string module, string? role, bool authenticated = true, string claimType = "role")
        {
            var claims = role is null ? Array.Empty<Claim>() : new[] { new Claim(claimType, role) };
            var user = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "test" : null));
            var requirement = new RequireModuleRequirement(module);
            var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);
            await handler.HandleAsync(context);
            return context.HasSucceeded;
        }

        Assert.True(await IsAllowed(Modules.Actions, "actionparty"));
        Assert.True(await IsAllowed(Modules.Actions, "ACTIONPARTY", claimType: ClaimTypes.Role));
        Assert.False(await IsAllowed(Modules.Findings, "ActionParty"));
        Assert.False(await IsAllowed(Modules.Actions, "Unknown"));
        Assert.False(await IsAllowed(Modules.Actions, null));
        Assert.True(await IsAllowed(Modules.Actions, "gra"));
        Assert.True(await IsAllowed(Modules.Actions, "SUPERADMIN"));
        Assert.False(await IsAllowed(Modules.Actions, "GRA", authenticated: false));
    }
}
