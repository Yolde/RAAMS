using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RAAMS.Infrastructure.Data;

namespace RAAMS.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(RaamsDbContext db) : ControllerBase
{
    public record UserLookupDto(Guid Id, string DisplayName, string Email);

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserLookupDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserLookupDto>>> List(CancellationToken ct) =>
        Ok(await db.Users.AsNoTracking()
            .Where(u => u.IsActive && !u.IsDeleted)
            .OrderBy(u => u.DisplayName)
            .Select(u => new UserLookupDto(u.Id, u.DisplayName, u.Email))
            .ToListAsync(ct));
}
