using FleetService.Api.Dtos;
using FleetService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace FleetService.Api.Controllers;

[ApiController, Authorize, Route("api/reports")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ReportsController(IReportingService reports, ILogger<ReportsController> logger) : ControllerBase
{
    [HttpGet("fleet-summary"), Authorize(Roles = "ADMIN")]
    public Task<ActionResult<FleetSummaryResponse>> GetFleetSummary(CancellationToken ct) =>
        Read(() => reports.GetFleetSummaryAsync(ct));

    [HttpGet("bookings"), Authorize(Roles = "ADMIN")]
    public Task<ActionResult<BookingReportResponse>> GetBookings([FromQuery] BookingReportQuery query, CancellationToken ct) =>
        Read(() => reports.GetBookingReportAsync(query, ct));

    [HttpGet("maintenance"), Authorize(Roles = "ADMIN,FLEET_MANAGER,MAINTENANCE_STAFF")]
    public Task<ActionResult<MaintenanceReportResponse>> GetMaintenance(CancellationToken ct) =>
        Read(() => reports.GetMaintenanceReportAsync(ct));

    [HttpGet("operational-statistics"), Authorize(Roles = "ADMIN")]
    public Task<ActionResult<OperationalStatisticsResponse>> GetOperationalStatistics(CancellationToken ct) =>
        Read(() => reports.GetOperationalStatisticsAsync(ct));

    [HttpGet("bookings/export"), Authorize(Roles = "ADMIN")]
    public Task<IActionResult> ExportBookings([FromQuery] BookingExportQuery query, CancellationToken ct) =>
        Export(async () => ReportCsv.Bookings(await reports.GetBookingExportAsync(query, ct)), "fleetflow-bookings.csv");

    [HttpGet("maintenance/export"), Authorize(Roles = "ADMIN,FLEET_MANAGER,MAINTENANCE_STAFF")]
    public Task<IActionResult> ExportMaintenance(CancellationToken ct) =>
        Export(async () => ReportCsv.Maintenance(await reports.GetMaintenanceReportAsync(ct)), "fleetflow-maintenance.csv");

    private async Task<IActionResult> Export(Func<Task<byte[]>> read, string filename)
    {
        try { return File(await read(), "text/csv; charset=utf-8", filename); }
        catch (ArgumentException) { return BadRequest(new { message = "Invalid export parameters or more than 10,000 matching rows." }); }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException or RentalUnavailableException)
        {
            logger.LogWarning(ex, "Report export unavailable.");
            return Problem(statusCode: 503, title: "The report could not be exported. Please try again.");
        }
    }

    private async Task<ActionResult<T>> Read<T>(Func<Task<T>> query)
    {
        try { return Ok(await query()); }
        catch (ArgumentException) { return BadRequest(new { message = "Invalid report parameters." }); }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
        {
            logger.LogWarning(ex, "Persisted reporting data unavailable.");
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Reporting data is unavailable. Please try again.");
        }
    }
}
