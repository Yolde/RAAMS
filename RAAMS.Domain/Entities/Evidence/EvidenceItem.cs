using RAAMS.Domain.Common;
using RAAMS.Domain.Entities.Actions;
using RAAMS.Domain.Entities.Users;

namespace RAAMS.Domain.Entities.Evidence;

public class EvidenceItem : IEntity, ISoftDelete
{
    public Guid Id { get; set; }
    public Guid ActionId { get; set; }
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }
    public string SharePointItemId { get; set; } = null!;
    public string SharePointUrl { get; set; } = null!;
    public int Version { get; set; }
    public string FileHash { get; set; } = null!;
    public Guid UploadedById { get; set; }
    public DateTime UploadedAt { get; set; }
    public bool IsDeleted { get; set; }

    public ActionEntity Action { get; set; } = null!;
    public User UploadedBy { get; set; } = null!;
}
