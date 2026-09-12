using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IdentityService.Api.Dtos;
using IdentityService.Api.Exceptions;
using IdentityService.Api.Services;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IRegistrationService _registrationService;
    private readonly IAuthenticationService _authenticationService;
    private readonly IProfileImageService _profileImageService;

    public AuthController(
        IRegistrationService registrationService,
        IAuthenticationService authenticationService,
        IProfileImageService? profileImageService = null)
    {
        _registrationService = registrationService;
        _authenticationService = authenticationService;
        _profileImageService = profileImageService!;
    }

    [HttpPost("register")]
    public async Task<ActionResult<UserResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var response = await _registrationService.RegisterAsync(request);
            return Created(string.Empty, response);
        }
        catch (DuplicateException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var response = await _authenticationService.LoginAsync(request);
            return Ok(response);
        }
        catch (InvalidCredentialsException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (PortalAccessDeniedException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { 
                message = "Authentication service error: " + (ex.InnerException?.Message ?? ex.Message),
                detail = ex.ToString()
            });
        }
    }

    [Authorize]
    [HttpPost("profile-picture")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadProfilePicture([FromForm] IFormFile file)
    {
        if (file == null)
        {
            return BadRequest(new { message = "An image file must be provided." });
        }

        var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "User identity claim could not be established." });
        }

        try
        {
            var profileImageUrl = await _profileImageService.UploadProfilePictureAsync(userId, file);
            return Ok(new { profileImageUrl });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("profile-picture")]
    public async Task<IActionResult> GetProfilePicture()
    {
        var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "User identity claim could not be established." });
        }

        try
        {
            var profileImageUrl = await _profileImageService.GetProfilePictureUrlAsync(userId);
            return Ok(new { profileImageUrl });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpDelete("profile-picture")]
    public async Task<IActionResult> DeleteProfilePicture()
    {
        var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "User identity claim could not be established." });
        }

        try
        {
            await _profileImageService.DeleteProfilePictureAsync(userId);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
