using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RAAMS.Domain.Entities.Findings;

namespace RAAMS.Infrastructure.Configurations;

public class FindingConfiguration : IEntityTypeConfiguration<Finding>
{
    public void Configure(EntityTypeBuilder<Finding> builder)
    {
        builder.ToTable("Findings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(e => e.ReferenceNumber).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Description).IsRequired();
        builder.Property(e => e.RootCause).IsRequired();
        builder.Property(e => e.Recommendation).IsRequired();
        builder.Property(e => e.Severity).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.ModifiedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasIndex(e => e.ReferenceNumber).IsUnique().HasDatabaseName("UQ_Findings_ReferenceNumber");
        builder.HasIndex(e => new { e.AssessmentId, e.Status }).HasDatabaseName("IX_Findings_AssessmentId_Status");
        builder.HasOne(e => e.Assessment).WithMany(e => e.Findings).HasForeignKey(e => e.AssessmentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
