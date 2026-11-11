using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RAAMS.Domain.Entities.Users;

namespace RAAMS.Infrastructure.Configurations;

public class DelegationConfiguration : IEntityTypeConfiguration<Delegation>
{
    public void Configure(EntityTypeBuilder<Delegation> builder)
    {
        builder.ToTable("Delegations", t =>
        {
            t.HasCheckConstraint("CK_Delegations_EffectiveDates", "[EffectiveTo] > [EffectiveFrom]");
            t.HasCheckConstraint("CK_Delegations_DistinctUsers", "[DelegatorUserId] <> [DelegateUserId]");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
        builder.Property(e => e.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        builder.HasOne(e => e.DelegatorUser).WithMany(e => e.DelegationsGiven).HasForeignKey(e => e.DelegatorUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(e => e.DelegateUser).WithMany(e => e.DelegationsReceived).HasForeignKey(e => e.DelegateUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
