using System.ComponentModel.DataAnnotations;

namespace IdentityService.Api.Dtos;

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "New password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
    [StringLength(128)]
    public string NewPassword { get; set; } = string.Empty;

    [StringLength(1024)] public string? CurrentPassword { get; set; }
}
