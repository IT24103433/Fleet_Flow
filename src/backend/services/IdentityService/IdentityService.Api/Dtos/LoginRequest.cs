using System.ComponentModel.DataAnnotations;

namespace IdentityService.Api.Dtos;

public class LoginRequest
{
    [Required(ErrorMessage = "Username or email is required.")]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;
}
