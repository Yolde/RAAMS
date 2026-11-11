using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RAAMS.Domain.Entities.Actions;

namespace RAAMS.Infrastructure.Configurations;

public class ActionStatusHistoryConfiguration : IEntityTypeConfiguration<ActionStatusHistory>
{
    public void Configure(EntityTypeBuilder<ActionStatusHistory> builder)
    {
        builder.ToTable("ActionStatusHistory");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(e => e.FromStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.ToStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.ChangedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(e => new { e.ActionId, e.ToStatus }).HasDatabaseName("IX_ActionStatusHistory_ActionId_ToStatus");
        builder.HasOne(e => e.Action).WithMany(e => e.StatusHistory).HasForeignKey(e => e.ActionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(e => e.Actor).WithMany(e => e.ActionStatusChanges).HasForeignKey(e => e.ActorId).OnDelete(DeleteBehavior.NoAction);
    }
}
