using System.ComponentModel.DataAnnotations;

namespace RAAMS.API.Controllers;

public class UploadEscalationEmailRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;
    public string? Notes { get; set; }
}
