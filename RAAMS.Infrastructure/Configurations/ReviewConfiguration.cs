using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RAAMS.Domain.Entities.Reviews;

namespace RAAMS.Infrastructure.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(e => e.Outcome).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.ReasonCode).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.Comments).IsRequired();
        builder.Property(e => e.ReviewedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasOne(e => e.Action).WithMany(e => e.Reviews).HasForeignKey(e => e.ActionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(e => e.Reviewer).WithMany(e => e.Reviews).HasForeignKey(e => e.ReviewerId).OnDelete(DeleteBehavior.NoAction);
    }
}
