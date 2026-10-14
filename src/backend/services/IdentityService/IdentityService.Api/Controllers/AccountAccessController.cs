using FleetFlow.Contracts;
using FleetFlow.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace IdentityService.Api.Controllers;
[ApiController, Route("internal/accounts"), Authorize(Policy = ServiceScopes.IdentityAccessRead)]
public sealed class AccountAccessController(IAccountAccessReader accounts) : ControllerBase
{
    [HttpGet("{id:guid}/access")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (id == Guid.Empty) return BadRequest();
        try { var state = await accounts.ReadAsync(id, ct); return state == null ? NotFound(new { code = "account_not_found" }) : Ok(state); }
        catch (AccountAccessUnavailableException) { return StatusCode(503, new { message = "Account access state is unavailable." }); }
    }
}
