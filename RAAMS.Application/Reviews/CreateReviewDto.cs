namespace RAAMS.Application.Reviews;

public class CreateReviewDto
{
    public Guid ActionId { get; set; }
    public string Outcome { get; set; } = null!;
    public string? ReasonCode { get; set; }
    public string? Comments { get; set; }
}
