using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using RAAMS.Application.Auditing;
using RAAMS.Domain.Common;
using RAAMS.Domain.Entities.Auditing;
using RAAMS.Domain.Enums;

namespace RAAMS.Infrastructure.Auditing;

// Called explicitly by RaamsDbContext after soft-delete and timestamp handling.
// Do not also register this instance with AddInterceptors.
public class AuditSaveChangesInterceptor(
    IAuditContext auditContext,
    ILogger<AuditSaveChangesInterceptor> logger) : SaveChangesInterceptor
{
    private static bool IsAuditField(string name) =>
        name is not ("RowVersion" or "CreatedDate" or "ModifiedDate");

    public List<EntityEntry> GetChanges(DbContext context)
    {
        var changes = context.ChangeTracker.Entries()
            .Where(entry => entry.Entity is IEntity &&
                            entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in changes.Where(entry => entry.State == EntityState.Added))
        {
            var id = entry.Property(nameof(IEntity.Id));
            if (id.IsTemporary || (Guid)id.CurrentValue! == Guid.Empty)
            {
                id.CurrentValue = Guid.NewGuid();
                id.IsTemporary = false;
            }
        }

        context.ChangeTracker.DetectChanges();
        return changes;
    }

    public List<AuditTrail> Capture(DbContext context, IReadOnlyList<EntityEntry> entries, Guid? internalUserId)
    {
        Guid? correlationId = null;
        if (auditContext.CorrelationId is { } correlation)
        {
            if (!Guid.TryParse(correlation, out var parsed))
                throw new InvalidOperationException("The audit correlation ID must be a GUID.");
            correlationId = parsed;
        }

        var audits = new List<AuditTrail>(entries.Count);
        foreach (var entry in entries)
        {
            var idProperty = entry.Property(nameof(IEntity.Id));
            if (idProperty.IsTemporary || (Guid)idProperty.CurrentValue! == Guid.Empty)
                throw new InvalidOperationException($"Cannot audit {entry.Metadata.ClrType.Name} before its ID is available.");

            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Insert,
                EntityState.Modified => AuditAction.Update,
                EntityState.Deleted => AuditAction.Delete,
                _ => throw new InvalidOperationException("Cannot audit an unchanged entity.")
            };
            audits.Add(new AuditTrail
            {
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = (Guid)idProperty.CurrentValue!,
                Action = action,
                OldValue = entry.State == EntityState.Modified ? SerializeValues(entry.OriginalValues) : null,
                NewValue = entry.State != EntityState.Deleted ? SerializeValues(entry.CurrentValues) : null,
                ChangedFields = entry.State == EntityState.Modified
                    ? string.Join(",", entry.Properties.Where(property => property.IsModified &&
                        !property.Metadata.IsPrimaryKey() && IsAuditField(property.Metadata.Name))
                        .Select(property => property.Metadata.Name))
                    : null,
                UserId = internalUserId,
                UserEmail = auditContext.UserEmail,
                Timestamp = DateTime.UtcNow,
                CorrelationId = correlationId,
                Reason = auditContext.Reason
            });
        }

        context.Set<AuditTrail>().AddRange(audits);
        logger.LogDebug("Captured {Count} audit entries", audits.Count);
        return audits;
    }

    private static string SerializeValues(PropertyValues values) =>
        JsonSerializer.Serialize(values.Properties
            .Where(property => IsAuditField(property.Name))
            .ToDictionary(property => property.Name, property => values[property]));
}
