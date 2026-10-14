using System.Security.Claims;
using FleetService.Api.Dtos;
using FleetService.Api.Exceptions;
using FleetService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace FleetService.Api.Controllers;
[ApiController, Route("api/maintenance"), Authorize(Roles = "ADMIN,FLEET_MANAGER,MAINTENANCE_STAFF")]
public sealed class MaintenancePlanningController(IMaintenanceService maintenance, IMaintenanceStaffDirectory staff) : ControllerBase
{
    private Guid Actor => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
    private async Task<IActionResult> Write(Func<Task<MaintenanceRecordResponse?>> action)
    {
        try { var record = await action(); return record == null ? NotFound(new { message = "Maintenance record was not found." }) : Ok(record); }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (DuplicateException ex) { return Conflict(new { message = ex.Message }); }
        catch (MaintenanceStaffUnavailableException) { return StatusCode(503, new { message = "Staff validation is unavailable. No assignment was saved. Try again when Identity is available." }); }
    }
    [HttpPut("records/{id:guid}/schedule")]
    public Task<IActionResult> Schedule(Guid id, MaintenanceScheduleRequest request, CancellationToken ct) => Write(() => maintenance.ScheduleAsync(id, Actor, request, ct));
    [HttpPut("records/{id:guid}/assignment"), Authorize(Roles = "ADMIN,FLEET_MANAGER")]
    public Task<IActionResult> Assign(Guid id, MaintenanceAssignmentRequest request, CancellationToken ct) => Write(() => maintenance.AssignAsync(id, Actor, request, ct));
    [HttpGet("records/{id:guid}/audit")]
    public async Task<IActionResult> Audit(Guid id, CancellationToken ct)
    { var entries = await maintenance.AuditAsync(id, ct); return entries == null ? NotFound() : Ok(entries); }
    [HttpGet("staff"), Authorize(Roles = "ADMIN,FLEET_MANAGER")]
    public async Task<IActionResult> Staff(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        try { return Ok(await staff.ListAsync(ct)); }
        catch (MaintenanceStaffUnavailableException) { return StatusCode(503, new { message = "The active Maintenance Staff directory is unavailable." }); }
    }
}
