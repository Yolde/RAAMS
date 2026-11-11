using Microsoft.AspNetCore.Authorization;

namespace RAAMS.API.Authorization;

public class RequireModuleAttribute : AuthorizeAttribute
{
    public RequireModuleAttribute(string module) : base($"RequireModule:{module}")
    {
    }
}
