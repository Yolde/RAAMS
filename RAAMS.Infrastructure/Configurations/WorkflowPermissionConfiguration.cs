using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RAAMS.Domain.Entities.Roles;

namespace RAAMS.Infrastructure.Configurations;

public class WorkflowPermissionConfiguration : IEntityTypeConfiguration<WorkflowPermission>
{
    public void Configure(EntityTypeBuilder<WorkflowPermission> builder)
    {
        builder.ToTable("WorkflowPermissions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(e => e.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.CurrentStatus).HasMaxLength(100).IsRequired();
        builder.Property(e => e.TargetStatus).HasMaxLength(100).IsRequired();
        builder.Property(e => e.CanView).HasDefaultValue(false);
        builder.Property(e => e.CanEdit).HasDefaultValue(false);
        builder.Property(e => e.CanApprove).HasDefaultValue(false);
        builder.Property(e => e.CanReopen).HasDefaultValue(false);
        builder.Property(e => e.CanDelete).HasDefaultValue(false);
        builder.HasIndex(e => new { e.RoleId, e.EntityType, e.CurrentStatus, e.TargetStatus }).IsUnique().HasDatabaseName("UQ_WorkflowPermissions_Transition");
        builder.HasOne(e => e.Role).WithMany(e => e.WorkflowPermissions).HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}
