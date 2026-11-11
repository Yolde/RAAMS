using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RAAMS.Domain.Entities.Assessments;

namespace RAAMS.Infrastructure.Configurations;

public class AssessmentStatusHistoryConfiguration : IEntityTypeConfiguration<AssessmentStatusHistory>
{
    public void Configure(EntityTypeBuilder<AssessmentStatusHistory> builder)
    {
        builder.ToTable("AssessmentStatusHistory");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(e => e.FromStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.ToStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.ChangedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasOne(e => e.Assessment).WithMany(e => e.StatusHistory).HasForeignKey(e => e.AssessmentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(e => e.Actor).WithMany(e => e.AssessmentStatusChanges).HasForeignKey(e => e.ActorId).OnDelete(DeleteBehavior.NoAction);
    }
}
