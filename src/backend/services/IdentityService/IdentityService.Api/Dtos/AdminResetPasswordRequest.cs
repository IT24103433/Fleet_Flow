namespace IdentityService.Api.Dtos;

public class AdminResetPasswordRequest
{
    public string? CustomTemporaryPassword { get; set; }
    public bool ForcePasswordChange { get; set; } = true;
}
