using System.ComponentModel.DataAnnotations;

namespace IdentityService.Api.Dtos;

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "New password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
    public string NewPassword { get; set; } = string.Empty;

    public string? CurrentPassword { get; set; }
}
