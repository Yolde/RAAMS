using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RAAMS.Domain.Entities.Evidence;

namespace RAAMS.Infrastructure.Configurations;

public class EvidenceContentConfiguration : IEntityTypeConfiguration<EvidenceContent>
{
    public void Configure(EntityTypeBuilder<EvidenceContent> builder)
    {
        builder.ToTable("EvidenceContents");
        builder.HasKey(content => content.EvidenceId);
        builder.Property(content => content.Data).HasColumnType("varbinary(max)").IsRequired();
        builder.HasOne(content => content.Evidence).WithOne()
            .HasForeignKey<EvidenceContent>(content => content.EvidenceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasQueryFilter(content => !content.Evidence.IsDeleted);
    }
}
