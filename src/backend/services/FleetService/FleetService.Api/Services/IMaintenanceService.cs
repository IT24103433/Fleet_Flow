using FleetService.Api.Dtos;
using FleetService.Api.Entities;

namespace FleetService.Api.Services;

public interface IMaintenanceService
{
    Task<MaintenanceRecordResponse?> UpdateMaintenanceRecordAsync(Guid id, Guid actor, UpdateMaintenanceRecordRequest request, CancellationToken ct = default) => UpdateMaintenanceRecordAsync(id, request, ct);
    Task<MaintenanceRecordResponse?> UpdateMaintenanceStatusAsync(Guid id, Guid actor, MaintenanceStatus status, CancellationToken ct = default) => UpdateMaintenanceStatusAsync(id, status, ct);
    Task<MaintenanceRecordResponse?> ScheduleAsync(Guid id, Guid actor, MaintenanceScheduleRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    Task<MaintenanceRecordResponse?> AssignAsync(Guid id, Guid actor, MaintenanceAssignmentRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    Task<IReadOnlyList<MaintenanceAuditEntry>?> AuditAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    Task<MaintenanceDashboardResponse> GetMaintenanceDashboardAsync(string? hub = null, CancellationToken cancellationToken = default);
    Task<MaintenanceRecordResponse> CreateMaintenanceRecordAsync(Guid createdByUserId, CreateMaintenanceRecordRequest request, CancellationToken cancellationToken = default);
    Task<MaintenanceRecordResponse?> GetMaintenanceRecordAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MaintenanceRecordResponse>> GetVehicleMaintenanceHistoryAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task<MaintenanceRecordResponse?> UpdateMaintenanceRecordAsync(Guid id, UpdateMaintenanceRecordRequest request, CancellationToken cancellationToken = default);
    Task<MaintenanceRecordResponse?> UpdateMaintenanceStatusAsync(Guid id, MaintenanceStatus status, CancellationToken cancellationToken = default);
}
