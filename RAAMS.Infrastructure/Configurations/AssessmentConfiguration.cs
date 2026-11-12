using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RAAMS.Domain.Entities.Assessments;
using RAAMS.Domain.Enums;

namespace RAAMS.Infrastructure.Configurations;

public class AssessmentConfiguration : IEntityTypeConfiguration<Assessment>
{
    public void Configure(EntityTypeBuilder<Assessment> builder)
    {
        builder.ToTable("Assessments", t => t.HasCheckConstraint("CK_Assessments_Quarter", "[Quarter] IN ('Q1', 'Q2', 'Q3', 'Q4')"));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(e => e.ReferenceNumber).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(255).IsRequired();
        builder.Property(e => e.Scope).IsRequired();
        builder.Property(e => e.Function).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Quarter).HasColumnType("char(2)").IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.LineOfDefence).HasConversion<string>().HasMaxLength(30).HasDefaultValue(LineOfDefence.LOD1);
        builder.Property(e => e.Opinion).HasConversion<string>().HasMaxLength(30)
            .HasDefaultValue(AssessmentOpinion.NotRated).HasSentinel(AssessmentOpinion.NotRated);
        builder.Property(e => e.CancellationReason).HasMaxLength(500);
        builder.Property(e => e.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.ModifiedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasIndex(e => e.ReferenceNumber).IsUnique().HasDatabaseName("UQ_Assessments_ReferenceNumber");
        builder.HasIndex(e => new { e.Status, e.Function }).HasDatabaseName("IX_Assessments_Status_Function");
        builder.HasIndex(e => e.LineOfDefence);
        builder.HasOne(e => e.LeadAuditor).WithMany(e => e.LeadAuditorAssessments).HasForeignKey(e => e.LeadAuditorId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(e => e.Coordinator).WithMany(e => e.CoordinatorAssessments).HasForeignKey(e => e.CoordinatorId).OnDelete(DeleteBehavior.NoAction);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
