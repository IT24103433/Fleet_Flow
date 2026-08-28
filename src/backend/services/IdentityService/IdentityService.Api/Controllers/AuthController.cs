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

    public AuthController(IRegistrationService registrationService)
    {
        _registrationService = registrationService;
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
}
