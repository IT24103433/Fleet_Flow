using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FleetService.Api.Dtos;
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
}
