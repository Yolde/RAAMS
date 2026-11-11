using RAAMS.Domain.Common;
using RAAMS.Domain.Entities.Users;
using RAAMS.Domain.Enums;

namespace RAAMS.Domain.Entities.Notifications;

public class Notification : IEntity
{
    public Guid Id { get; set; }
    public string RecipientEmail { get; set; } = null!;
    public Guid? RecipientUserId { get; set; }
    public string Subject { get; set; } = null!;
    public string Body { get; set; } = null!;
    public NotificationChannel Channel { get; set; }
    public string EventType { get; set; } = null!;
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public DateTime? SentAt { get; set; }
    public string DeliveryStatus { get; set; } = null!;
    public string? ErrorMessage { get; set; }

    public User? RecipientUser { get; set; }
}
