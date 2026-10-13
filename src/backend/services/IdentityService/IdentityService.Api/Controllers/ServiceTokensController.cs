using FleetFlow.Contracts;
using FleetFlow.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("internal/service-token")]
public sealed class ServiceTokensController(ServiceTokenIssuer issuer,
    ServiceAuthenticationOptions options, IHostEnvironment environment) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost]
    [RequestSizeLimit(4096)]
    [EnableRateLimiting("service-token")]
    public ActionResult<ServiceTokenResponse> Create(ServiceTokenRequest request)
    {
        Response.Headers.CacheControl = "no-store";
        if (!options.Enabled) return StatusCode(503, new { message = "Service authentication is not enabled." });
        if (!environment.IsDevelopment() && !Request.IsHttps)
            return BadRequest(new { message = "Service authentication requires HTTPS." });
        var response = issuer.Issue(request);
        return response is null ? Unauthorized(new { message = "Invalid service credentials." }) : Ok(response);
    }
}
