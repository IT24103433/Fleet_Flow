namespace IdentityService.Api.Dtos;

public class AdminResetPasswordResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TemporaryPassword { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; } = true;
    public DateTime ResetAt { get; set; } = DateTime.UtcNow;
    public string Message { get; set; } = string.Empty;
}
