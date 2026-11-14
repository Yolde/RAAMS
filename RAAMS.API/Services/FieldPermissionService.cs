using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RAAMS.Application.Authorization;
using RAAMS.Infrastructure.Data;

namespace RAAMS.API.Services;

public class FieldPermissionService(
    RaamsDbContext dbContext,
    IMemoryCache cache,
    ILogger<FieldPermissionService> logger) : IFieldPermissionService
{
    private readonly record struct CacheKey(string EntityType, string FieldName, string Roles);

    public async Task<FieldPermissionResult> GetAsync(
        string entityType,
        string fieldName,
        IEnumerable<string> userRoles,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentNullException.ThrowIfNull(userRoles);
        ct.ThrowIfCancellationRequested();

        var roles = userRoles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray();

        if (roles.Any(role => string.Equals(role, "GRA", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase)))
        {
            var adminResult = new FieldPermissionResult(true, true);
            LogResult(entityType, fieldName, adminResult);
            return adminResult;
        }

        var key = new CacheKey(entityType, fieldName, JsonSerializer.Serialize(roles));
        if (cache.TryGetValue(key, out FieldPermissionResult? cached) && cached is not null)
        {
            LogResult(entityType, fieldName, cached);
            return cached;
        }

        var permissions = await dbContext.FieldPermissions.AsNoTracking()
            .Where(permission => permission.EntityType == entityType &&
                                 permission.FieldName == fieldName &&
                                 roles.Contains(permission.Role.Name))
            .Select(permission => new { permission.CanView, permission.CanEdit })
            .ToListAsync(ct);

        var result = new FieldPermissionResult(
            permissions.Any(permission => permission.CanView),
            permissions.Any(permission => permission.CanEdit));
        cache.Set(key, result, TimeSpan.FromMinutes(5));
        LogResult(entityType, fieldName, result);
        return result;
    }

    public async Task<IReadOnlyList<FieldPermissionDto>> ListAllAsync(CancellationToken ct = default)
    {
        return await dbContext.FieldPermissions
            .AsNoTracking()
            .Include(p => p.Role)
            .OrderBy(p => p.EntityType)
            .ThenBy(p => p.FieldName)
            .ThenBy(p => p.Role.Name)
            .Select(p => new FieldPermissionDto
            {
                Id = p.Id,
                RoleId = p.RoleId,
                RoleName = p.Role.Name,
                EntityType = p.EntityType,
                FieldName = p.FieldName,
                CanView = p.CanView,
                CanEdit = p.CanEdit,
            })
            .ToListAsync(ct);
    }

    public async Task<FieldPermissionDto> UpsertAsync(UpsertFieldPermissionDto dto, CancellationToken ct = default)
    {
        var existing = await dbContext.FieldPermissions
            .Include(p => p.Role)
            .FirstOrDefaultAsync(p => p.RoleId == dto.RoleId
                                   && p.EntityType == dto.EntityType
                                   && p.FieldName == dto.FieldName, ct);

        if (existing is null)
        {
            existing = new Domain.Entities.Roles.FieldPermission
            {
                Id = Guid.NewGuid(),
                RoleId = dto.RoleId,
                EntityType = dto.EntityType,
                FieldName = dto.FieldName,
                CanView = dto.CanView,
                CanEdit = dto.CanEdit,
            };
            dbContext.FieldPermissions.Add(existing);
            await dbContext.Entry(existing).Reference(p => p.Role).LoadAsync(ct);
        }
        else
        {
            existing.CanView = dto.CanView;
            existing.CanEdit = dto.CanEdit;
        }

        await dbContext.SaveChangesAsync(ct);

        // Invalidate the cache — critical! Otherwise the old permission sticks for 5 minutes.
        if (cache is MemoryCache mc)
            mc.Clear();

        return new FieldPermissionDto
        {
            Id = existing.Id,
            RoleId = existing.RoleId,
            RoleName = existing.Role.Name,
            EntityType = existing.EntityType,
            FieldName = existing.FieldName,
            CanView = existing.CanView,
            CanEdit = existing.CanEdit,
        };
    }

    private void LogResult(string entityType, string fieldName, FieldPermissionResult result)
    {
        logger.LogDebug(
            "Field permission for {EntityType}.{FieldName}: CanView={CanView}, CanEdit={CanEdit}",
            entityType, fieldName, result.CanView, result.CanEdit);
    }
}