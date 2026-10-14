using FleetFlow.Contracts;
using IdentityService.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace IdentityService.Api.Controllers;
[ApiController, Route("internal/staff/maintenance"), Authorize(Policy = ServiceScopes.IdentityStaffRead)]
public sealed class MaintenanceStaffController(IdentityDbContext db) : ControllerBase
{
    private IQueryable<IdentityService.Api.Entities.User> Active() => db.Users.AsNoTracking()
        .Where(u => u.IsActive && u.Roles.Any(r => r.Name == "MAINTENANCE_STAFF"));
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var accounts = await Active().OrderBy(u => u.FullName).ThenBy(u => u.Username).Take(1001)
            .Select(u => new MaintenanceStaffAccount(u.Id, u.FullName, u.Username)).ToListAsync(ct);
        return accounts.Count > 1000 ? StatusCode(503, new { message = "Staff directory exceeds the supported limit." }) : Ok(accounts);
    }
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var account = await Active().Where(u => u.Id == id).Select(u => new MaintenanceStaffAccount(u.Id, u.FullName, u.Username)).SingleOrDefaultAsync(ct);
        return account == null ? NotFound() : Ok(account);
    }
}
