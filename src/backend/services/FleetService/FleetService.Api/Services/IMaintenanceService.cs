using FleetService.Api.Dtos;

namespace FleetService.Api.Services;

public interface IMaintenanceService
{
    Task<MaintenanceDashboardResponse> GetMaintenanceDashboardAsync(string? hub = null, CancellationToken cancellationToken = default);
}
