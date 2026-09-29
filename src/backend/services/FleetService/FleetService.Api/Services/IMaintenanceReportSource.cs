using FleetService.Api.Dtos;

namespace FleetService.Api.Services;

public interface IMaintenanceReportSource
{
    // Replace with an AsNoTracking projection over PR #26's persisted work orders and history.
    Task<MaintenanceRecordsSnapshot> ReadAsync(CancellationToken ct = default);
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
