using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RAAMS.Domain.Entities.Auditing;

namespace RAAMS.Infrastructure.Configurations;

public class AuditTrailConfiguration : IEntityTypeConfiguration<AuditTrail>
{
    public void Configure(EntityTypeBuilder<AuditTrail> builder)
    {
        builder.ToTable("AuditTrails");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.Property(e => e.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Action).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.UserEmail).HasMaxLength(255);
        builder.Property(e => e.Timestamp).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.HasIndex(e => new { e.EntityType, e.EntityId, e.Timestamp }).HasDatabaseName("IX_AuditTrails_EntityType_EntityId_Timestamp");
        builder.HasOne(e => e.User).WithMany(e => e.AuditTrails).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
