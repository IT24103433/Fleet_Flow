using FleetService.Api.Dtos;
using FleetService.Api.Entities;

namespace FleetService.Api.Services;

public interface IMaintenanceService
{
    Task<MaintenanceDashboardResponse> GetMaintenanceDashboardAsync(string? hub = null, CancellationToken cancellationToken = default);
    Task<MaintenanceRecordResponse> CreateMaintenanceRecordAsync(Guid createdByUserId, CreateMaintenanceRecordRequest request, CancellationToken cancellationToken = default);
    Task<MaintenanceRecordResponse?> GetMaintenanceRecordAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MaintenanceRecordResponse>> GetVehicleMaintenanceHistoryAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task<MaintenanceRecordResponse?> UpdateMaintenanceRecordAsync(Guid id, UpdateMaintenanceRecordRequest request, CancellationToken cancellationToken = default);
    Task<MaintenanceRecordResponse?> UpdateMaintenanceStatusAsync(Guid id, MaintenanceStatus status, CancellationToken cancellationToken = default);
}
