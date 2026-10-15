using FleetFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentalService.Api.Services;

namespace RentalService.Api.Controllers;
[ApiController,Route("internal"),ServiceFilter(typeof(RentalActivationFilter))]
public sealed class AccountLifecycleController(AccountLifecycleService accounts):ControllerBase
{
    [HttpGet("account-obligations/{customer:guid}"),Authorize(Policy=OnboardingScopes.AccountObligations)]
    public async Task<IActionResult> Obligations(Guid customer,CancellationToken ct)=>Ok(new AccountObligations(await accounts.Outstanding(customer,ct)));
    [HttpPost("accounts/{customer:guid}/freeze"),Authorize(Policy=OnboardingScopes.RentalLifecycle)]
    public async Task<IActionResult> Freeze(Guid customer,AccountLifecycleCommand input,CancellationToken ct)=>Ok(await accounts.Freeze(customer,input,ct));
    [HttpPost("accounts/{customer:guid}/restore"),Authorize(Policy=OnboardingScopes.RentalLifecycle)]
    public async Task<IActionResult> Restore(Guid customer,AccountLifecycleCommand input,CancellationToken ct)=>Ok(await accounts.Release(customer,input,ct));
    [HttpPost("accounts/{customer:guid}/anonymize"),Authorize(Policy=OnboardingScopes.RentalLifecycle)]
    public async Task<IActionResult> Anonymize(Guid customer,AccountLifecycleCommand input,CancellationToken ct)=>Ok(await accounts.Anonymize(customer,input,ct));
}
