using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RAAMS.Domain.Entities.Roles;

namespace RAAMS.Infrastructure.Configurations;

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles", t => t.HasCheckConstraint("CK_UserRoles_EffectiveDates", "[EffectiveTo] IS NULL OR [EffectiveTo] > [EffectiveFrom]"));
        builder.HasKey(e => new { e.UserId, e.RoleId });
        builder.Property(e => e.EffectiveFrom).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasOne(e => e.User).WithMany(e => e.UserRoles).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Role).WithMany(e => e.UserRoles).HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}
