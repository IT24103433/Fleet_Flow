using System.ComponentModel.DataAnnotations;

namespace IdentityService.Api.Dtos;

public class LoginRequest
{
    [Required(ErrorMessage = "Username or email is required.")]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Login channel: "customer" or "staff".
    /// Determines which portal the user is authenticating through.
    /// Server enforces that role matches the requested channel.
    /// </summary>
    [Required(ErrorMessage = "Login channel is required.")]
    [RegularExpression("^(customer|staff)$", ErrorMessage = "Login channel must be 'customer' or 'staff'.")]
    public string LoginChannel { get; set; } = "customer";
}
