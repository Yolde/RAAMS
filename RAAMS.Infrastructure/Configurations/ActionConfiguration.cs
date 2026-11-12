using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RAAMS.Domain.Entities.Actions;
using RAAMS.Domain.Enums;

namespace RAAMS.Infrastructure.Configurations;

public class ActionConfiguration : IEntityTypeConfiguration<ActionEntity>
{
    public void Configure(EntityTypeBuilder<ActionEntity> builder)
    {
        builder.ToTable("Actions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(e => e.ReferenceNumber).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ActionDescription).IsRequired();
        builder.Property(e => e.ActionRequired).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.Reportable).HasConversion<string>().HasMaxLength(20)
            .HasDefaultValue(Reportable.NonCEO).HasSentinel(Reportable.NonCEO);
        builder.Property(e => e.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.ModifiedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasIndex(e => e.ReferenceNumber).IsUnique().HasDatabaseName("UQ_Actions_ReferenceNumber");
        builder.HasIndex(e => new { e.ActionPartyId, e.Status }).HasDatabaseName("IX_Actions_ActionPartyId_Status");
        builder.HasIndex(e => new { e.TargetDate, e.Status }).HasDatabaseName("IX_Actions_TargetDate_Status");
        builder.HasIndex(e => e.Reportable);
        builder.HasIndex(e => e.TargetDate, "IX_Actions_Overdue_Open").HasFilter("[Status] = N'Open' AND [IsDeleted] = 0").IncludeProperties(e => new { e.Status, e.IsDeleted });
        builder.HasIndex(e => e.TargetDate, "IX_Actions_Overdue_Submitted").HasFilter("[Status] = N'Submitted' AND [IsDeleted] = 0").IncludeProperties(e => new { e.Status, e.IsDeleted });
        builder.HasIndex(e => e.TargetDate, "IX_Actions_Overdue_Reopened").HasFilter("[Status] = N'Reopened' AND [IsDeleted] = 0").IncludeProperties(e => new { e.Status, e.IsDeleted });
        builder.HasOne(e => e.Finding).WithMany(e => e.Actions).HasForeignKey(e => e.FindingId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(e => e.ActionParty).WithMany(e => e.ActionPartyActions).HasForeignKey(e => e.ActionPartyId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(e => e.Auditee).WithMany(e => e.AuditeeActions).HasForeignKey(e => e.AuditeeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
