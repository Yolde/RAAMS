using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAAMS.Application.Authorization;

namespace RAAMS.API.Controllers;

[ApiController]
[Route("api/field-permissions")]
[Authorize(Roles = "GRA,SuperAdmin")]
public class FieldPermissionsController(IFieldPermissionService fieldPermissions) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FieldPermissionDto>>> List(CancellationToken ct) =>
        Ok(await fieldPermissions.ListAllAsync(ct));

    [HttpPut]
    public async Task<ActionResult<FieldPermissionDto>> Upsert(
        [FromBody] UpsertFieldPermissionDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.EntityType) || string.IsNullOrWhiteSpace(dto.FieldName))
            return BadRequest(new { error = "EntityType and FieldName are required." });
        return Ok(await fieldPermissions.UpsertAsync(dto, ct));
    }
}