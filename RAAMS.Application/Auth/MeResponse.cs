namespace RAAMS.Application.Auth;

public class MeResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string Function { get; set; } = null!;
    public string Department { get; set; } = null!;
    public List<string> Roles { get; set; } = new List<string>();
    public List<string> Permissions { get; set; } = new List<string>();
    public DateTime TokenExpiresAt { get; set; }
}
