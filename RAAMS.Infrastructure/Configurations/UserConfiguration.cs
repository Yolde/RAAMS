using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RAAMS.Domain.Entities.Users;

namespace RAAMS.Infrastructure.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(e => e.DisplayName).HasMaxLength(255).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(255).IsRequired();
        builder.Property(e => e.Function).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Department).HasMaxLength(100).IsRequired();
        builder.Property(e => e.IsActive).HasDefaultValue(true);
        builder.Property(e => e.CreatedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.ModifiedDate).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        builder.Property(e => e.RowVersion).IsRowVersion();
        builder.HasIndex(e => e.EntraObjectId).IsUnique().HasDatabaseName("UQ_Users_EntraObjectId");
        builder.HasIndex(e => e.Email).IsUnique().HasDatabaseName("UQ_Users_Email");
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
