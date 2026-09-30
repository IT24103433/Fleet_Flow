using FleetService.Api.Data;
using FleetService.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FleetService.Api.Services;

public interface IMaintenanceReportSource
{
    Task<MaintenanceRecordsSnapshot> ReadAsync(CancellationToken ct = default);
}

public class MaintenanceReportSource(FleetDbContext db) : IMaintenanceReportSource
{
    public async Task<MaintenanceRecordsSnapshot> ReadAsync(CancellationToken ct = default)
    {
        var records = await db.MaintenanceRecords
            .AsNoTracking()
            .Include(record => record.Vehicle)
            .OrderByDescending(record => record.ScheduledDateTime)
            .ThenByDescending(record => record.CreatedAt)
            .ToListAsync(ct);

        return new MaintenanceRecordsSnapshot(true, null, records.Select(record =>
            new MaintenanceRecordRow(
                record.Id,
                record.VehicleId,
                record.Vehicle?.LicensePlate,
                record.ServiceInformation,
                record.Status.ToString(),
                record.ScheduledDateTime,
                record.CompletedAt,
                record.Cost,
                [])).ToList());
    }
}

public class UnavailableMaintenanceReportSource : IMaintenanceReportSource
{
    public Task<MaintenanceRecordsSnapshot> ReadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new MaintenanceRecordsSnapshot(false,
            "Maintenance activity, history and cost records are not available yet. Current vehicle maintenance status is shown below.", []));
    }
}
