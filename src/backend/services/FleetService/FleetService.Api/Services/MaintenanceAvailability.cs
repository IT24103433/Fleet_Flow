using FleetService.Api.Data;
using FleetService.Api.Entities;
using Microsoft.EntityFrameworkCore;
namespace FleetService.Api.Services;
public static class MaintenanceAvailability
{
    // Half-open planned windows; actual work and unknown legacy ends stay conservative.
    public static IQueryable<MaintenanceRecord> Blocking(FleetDbContext db, Guid vehicleId, DateTime start, DateTime end, Guid? exclude = null) =>
        db.MaintenanceRecords.Where(r => r.VehicleId == vehicleId && (!exclude.HasValue || r.Id != exclude.Value) &&
            (r.Status == MaintenanceStatus.IN_PROGRESS || r.Status == MaintenanceStatus.SCHEDULED &&
                (!r.PlannedEndDateTime.HasValue || r.ScheduledDateTime < end && r.PlannedEndDateTime > start)));
    public static Task<bool> CurrentAsync(FleetDbContext db, Guid vehicleId, CancellationToken ct = default) =>
        db.MaintenanceRecords.AnyAsync(r => r.VehicleId == vehicleId && (r.Status == MaintenanceStatus.IN_PROGRESS ||
            r.Status == MaintenanceStatus.SCHEDULED && !r.PlannedEndDateTime.HasValue), ct);
    public static Task<bool> CurrentAsyncExcept(FleetDbContext db, Guid vehicleId, Guid except, CancellationToken ct = default) =>
        db.MaintenanceRecords.AnyAsync(r => r.VehicleId == vehicleId && r.Id != except && (r.Status == MaintenanceStatus.IN_PROGRESS ||
            r.Status == MaintenanceStatus.SCHEDULED && !r.PlannedEndDateTime.HasValue), ct);
}
