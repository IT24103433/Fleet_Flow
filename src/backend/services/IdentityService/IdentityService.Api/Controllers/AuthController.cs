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
    private readonly IdentityService.Api.Onboarding.OnboardingService? _onboarding;

    public AuthController(
        IRegistrationService registrationService,
        IAuthenticationService authenticationService,
        IProfileImageService? profileImageService = null,
        IdentityService.Api.Onboarding.OnboardingService? onboarding = null)
    {
        _registrationService = registrationService;
        _authenticationService = authenticationService;
        _profileImageService = profileImageService!;
        _onboarding = onboarding;
    }

    [HttpPost("register")]
    [RequestSizeLimit(65536)]
    public async Task<ActionResult<UserResponse>> Register([FromBody] RegisterRequest request)
    {
        if (_onboarding?.Enabled == true) return Conflict(new { message = "Use verified signup to create an account. Email verification is required." });
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
    [RequestSizeLimit(65536)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (_onboarding?.Enabled == true) return Ok(await _onboarding.Login(request));
            var response = await _authenticationService.LoginAsync(request);
            return Ok(response);
        }
        catch (InvalidCredentialsException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (IdentityService.Api.Onboarding.OnboardingException ex)
        {
            return StatusCode(ex.Status, new { message = ex.Message });
        }
        catch (AccountDisabledException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (PortalAccessDeniedException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Sign-in is temporarily unavailable. Please try again." });
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

    [Authorize]
    [HttpPost("change-password")]
    [RequestSizeLimit(65536)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { message = "User identity claim could not be established." });
        }

        try
        {
            await _authenticationService.ChangePasswordAsync(userId, request);
            var session = User?.HasClaim("must_change_password", "true") == true ? await _authenticationService.RenewSessionAsync(userId) : null;
            return Ok(new { message = "Password changed successfully.", token = session?.Token, expiration = session?.Expiration, user = session?.User });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (AccountDisabledException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
