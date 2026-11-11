using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RAAMS.Domain.Entities.Roles;

namespace RAAMS.Infrastructure.Configurations;

public class FieldPermissionConfiguration : IEntityTypeConfiguration<FieldPermission>
{
    public void Configure(EntityTypeBuilder<FieldPermission> builder)
    {
        builder.ToTable("FieldPermissions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(e => e.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.FieldName).HasMaxLength(128).IsRequired();
        builder.Property(e => e.CanView).HasDefaultValue(false);
        builder.Property(e => e.CanEdit).HasDefaultValue(false);
        builder.HasIndex(e => new { e.RoleId, e.EntityType, e.FieldName }).IsUnique().HasDatabaseName("UQ_FieldPermissions_Field");
        builder.HasOne(e => e.Role).WithMany(e => e.FieldPermissions).HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}
