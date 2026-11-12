using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RAAMS.Application.Auditing;
using RAAMS.Application.Services;
using RAAMS.Infrastructure.Auditing;
using RAAMS.Infrastructure.Data;

namespace RAAMS.Tests.Integration;

internal sealed class SqliteTestDbContext(
    DbContextOptions<RaamsDbContext> options,
    ICurrentUserService currentUser,
    IAuditContext audit,
    ILogger<AuditSaveChangesInterceptor> logger)
    : RaamsDbContext(options, currentUser, audit, logger)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnType(null);
                var defaultSql = property.GetDefaultValueSql();
                if (defaultSql == "NEWSEQUENTIALID()")
                    property.SetDefaultValueSql(null);
                else if (defaultSql == "SYSUTCDATETIME()")
                    property.SetDefaultValueSql("CURRENT_TIMESTAMP");

                if (property.Name == "RowVersion" && property.ClrType == typeof(byte[]))
                    property.SetDefaultValueSql("randomblob(8)");
            }

            foreach (var index in entity.GetIndexes())
            {
                var filter = index.GetFilter();
                if (filter is not null)
                    index.SetFilter(filter.Replace("N'", "'", StringComparison.Ordinal));
            }
        }
    }
}
