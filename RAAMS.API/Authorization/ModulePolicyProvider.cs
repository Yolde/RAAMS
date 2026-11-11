using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace RAAMS.API.Authorization;

public class ModulePolicyProvider : IAuthorizationPolicyProvider
{
    private const string Prefix = "RequireModule:";
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public ModulePolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            var module = policyName[Prefix.Length..];
            if (string.IsNullOrWhiteSpace(module))
                throw new InvalidOperationException("Module authorization policies require a module name.");

            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new RequireModuleRequirement(module))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        if (policyName is "RequireAuthenticatedUser" or "Default")
            return Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return _fallback.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
}
