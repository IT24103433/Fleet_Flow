using FleetService.Api.Dtos;

namespace FleetService.Api.Services;

public interface IReportingService
{
    Task<BookingExportResponse> GetBookingExportAsync(BookingExportQuery query, CancellationToken ct = default);
    Task<FleetSummaryResponse> GetFleetSummaryAsync(CancellationToken ct = default);
    Task<BookingReportResponse> GetBookingReportAsync(BookingReportQuery query, CancellationToken ct = default);
    Task<MaintenanceReportResponse> GetMaintenanceReportAsync(CancellationToken ct = default);
    Task<OperationalStatisticsResponse> GetOperationalStatisticsAsync(CancellationToken ct = default);
}
