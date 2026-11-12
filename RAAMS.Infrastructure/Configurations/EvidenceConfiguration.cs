using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RAAMS.Domain.Entities.Evidence;

namespace RAAMS.Infrastructure.Configurations;

public class EvidenceConfiguration : IEntityTypeConfiguration<EvidenceItem>
{
    public void Configure(EntityTypeBuilder<EvidenceItem> builder)
    {
        builder.ToTable("Evidence", t =>
        {
            t.HasCheckConstraint("CK_Evidence_SizeBytes", "[SizeBytes] >= 0");
            t.HasCheckConstraint("CK_Evidence_Version", "[Version] > 0");
            t.HasCheckConstraint("CK_Evidence_OneParent", "[ActionId] IS NOT NULL OR [AssessmentId] IS NOT NULL");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(e => e.FileName).HasMaxLength(255).IsRequired();
        builder.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.SharePointItemId).HasMaxLength(255);
        builder.Property(e => e.SharePointUrl).HasMaxLength(500);
        builder.Property(e => e.FileHash).HasColumnType("char(64)");
        builder.Property(e => e.UploadedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        builder.Property(e => e.ActionId).IsRequired(false);
        builder.HasOne(e => e.Action).WithMany(e => e.Evidence).HasForeignKey(e => e.ActionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(e => e.Assessment).WithMany().HasForeignKey(e => e.AssessmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.UploadedBy).WithMany(e => e.UploadedEvidence).HasForeignKey(e => e.UploadedById).OnDelete(DeleteBehavior.NoAction);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
