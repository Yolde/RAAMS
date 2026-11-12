namespace RAAMS.Application.Reviews;

public class ReviewDto
{
    public Guid Id { get; set; }
    public Guid ActionId { get; set; }
    public string ActionReference { get; set; } = null!;
    public Guid ReviewerId { get; set; }
    public string ReviewerName { get; set; } = null!;
    public string Outcome { get; set; } = null!;
    public string? ReasonCode { get; set; }
    public string Comments { get; set; } = null!;
    public DateTime ReviewedAt { get; set; }
}
