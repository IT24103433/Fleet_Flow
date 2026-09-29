using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using FleetService.Api.Dtos;
using FleetService.Api.Exceptions;
using FleetService.Api.Services;

namespace FleetService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "FLEET_MANAGER,ADMIN,MAINTENANCE_STAFF")]
public class MaintenanceController : ControllerBase
{
    private readonly IMaintenanceService _maintenanceService;

    public MaintenanceController(IMaintenanceService maintenanceService)
    {
        _maintenanceService = maintenanceService;
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(MaintenanceDashboardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<MaintenanceDashboardResponse>> GetDashboard(
        [FromQuery] string? hub = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _maintenanceService.GetMaintenanceDashboardAsync(hub, cancellationToken);
        return Ok(response);
    }

    [HttpPost("records")]
    [ProducesResponseType(typeof(MaintenanceRecordResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MaintenanceRecordResponse>> CreateRecord(
        [FromBody] CreateMaintenanceRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            return Unauthorized(new { message = "User identity claim missing or invalid." });
        }

        try
        {
            var record = await _maintenanceService.CreateMaintenanceRecordAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetRecord), new { id = record.Id }, record);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (DuplicateException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("records/{id:guid}", Name = "GetMaintenanceRecord")]
    [ProducesResponseType(typeof(MaintenanceRecordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MaintenanceRecordResponse>> GetRecord(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var record = await _maintenanceService.GetMaintenanceRecordAsync(id, cancellationToken);
        return record == null
            ? NotFound(new { message = $"Maintenance record with ID '{id}' was not found." })
            : Ok(record);
    }

    [HttpGet("records")]
    [ProducesResponseType(typeof(IReadOnlyList<MaintenanceRecordResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MaintenanceRecordResponse>>> GetVehicleHistory(
        [FromQuery] Guid vehicleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var records = await _maintenanceService.GetVehicleMaintenanceHistoryAsync(vehicleId, cancellationToken);
            return Ok(records);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("records/{id:guid}")]
    [ProducesResponseType(typeof(MaintenanceRecordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MaintenanceRecordResponse>> UpdateRecord(
        Guid id,
        [FromBody] UpdateMaintenanceRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var record = await _maintenanceService.UpdateMaintenanceRecordAsync(id, request, cancellationToken);
            return record == null
                ? NotFound(new { message = $"Maintenance record with ID '{id}' was not found." })
                : Ok(record);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("records/{id:guid}/status")]
    [ProducesResponseType(typeof(MaintenanceRecordResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MaintenanceRecordResponse>> UpdateRecordStatus(
        Guid id,
        [FromBody] UpdateMaintenanceStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Status == null)
        {
            return BadRequest(new { message = "Maintenance status is required." });
        }

        try
        {
            var record = await _maintenanceService.UpdateMaintenanceStatusAsync(id, request.Status.Value, cancellationToken);
            return record == null
                ? NotFound(new { message = $"Maintenance record with ID '{id}' was not found." })
                : Ok(record);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claimValue, out var userId) ? userId : Guid.Empty;
    }
}
