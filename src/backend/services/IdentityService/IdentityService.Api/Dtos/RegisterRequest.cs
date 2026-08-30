using System.ComponentModel.DataAnnotations;

namespace IdentityService.Api.Dtos;

public class RegisterRequest
{
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9\-_]+$", ErrorMessage = "Username can only contain letters, numbers, hyphens, and underscores.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Email is not a valid email address.")]
    [StringLength(100, ErrorMessage = "Email must not exceed 100 characters.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).+$", 
        ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character.")]
    public string Password { get; set; } = string.Empty;
}
