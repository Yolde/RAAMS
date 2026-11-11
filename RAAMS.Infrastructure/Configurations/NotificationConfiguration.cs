using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RAAMS.Domain.Entities.Notifications;

namespace RAAMS.Infrastructure.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(e => e.RecipientEmail).HasMaxLength(255).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Body).IsRequired();
        builder.Property(e => e.Channel).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.EventType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.RelatedEntityType).HasMaxLength(100);
        builder.Property(e => e.DeliveryStatus).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => new { e.RecipientEmail, e.SentAt }).HasDatabaseName("IX_Notifications_RecipientEmail_SentAt");
        builder.HasOne(e => e.RecipientUser).WithMany(e => e.Notifications).HasForeignKey(e => e.RecipientUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
