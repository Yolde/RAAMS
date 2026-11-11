using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using RAAMS.Infrastructure.Data;

namespace RAAMS.API.Authorization;

public class RequireModuleHandler(IServiceScopeFactory scopeFactory) : AuthorizationHandler<RequireModuleRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, RequireModuleRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return;

        var roles = context.User.FindAll("role")
            .Concat(context.User.FindAll(ClaimTypes.Role))
            .Select(claim => claim.Value)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.ToLowerInvariant())
            .Distinct()
            .ToArray();

        if (roles.Contains("gra") || roles.Contains("superadmin"))
        {
            context.Succeed(requirement);
            return;
        }

        if (roles.Length == 0)
            return;

        var httpContext = context.Resource as HttpContext ??
                          (context.Resource as AuthorizationFilterContext)?.HttpContext;
        var cancellationToken = httpContext?.RequestAborted ?? CancellationToken.None;
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RaamsDbContext>();
        var authorized = await db.RolePermissions.AsNoTracking()
            .AnyAsync(permission => roles.Contains(permission.Role.Name.ToLower()) &&
                                    permission.Permission.Module == requirement.Module,
                cancellationToken);

        if (authorized)
            context.Succeed(requirement);
    }
}
