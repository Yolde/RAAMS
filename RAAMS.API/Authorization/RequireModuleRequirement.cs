using Microsoft.AspNetCore.Authorization;

namespace RAAMS.API.Authorization;

public record RequireModuleRequirement(string Module) : IAuthorizationRequirement;
