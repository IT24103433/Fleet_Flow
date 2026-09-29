using System.ComponentModel.DataAnnotations;
using FleetService.Api.Entities;

namespace FleetService.Api.Dtos;

public record VehicleStatusSummary(int Total, int Available, int InUse, int Maintenance, int Retired);
public record FleetSummaryResponse(DateTime GeneratedAt, VehicleStatusSummary Vehicles,
    int UtilizationEligibleVehicles, int CurrentlyBookedVehicles, decimal? CurrentBookingUtilizationPercent);
public record BookingStatistics(int Total, int Pending, int Confirmed, int Cancelled, int Completed,
    int Active, decimal NonCancelledBookingValue);
public record BookingReportRow(Guid BookingId, Guid CustomerId, Guid VehicleId, string? LicensePlate,
    string? Make, string? Model, BookingStatus Status, DateTime StartDateTime, DateTime EndDateTime,
    decimal TotalCost, DateTime CreatedAt, DateTime? UpdatedAt);
public record BookingReportResponse(DateTime GeneratedAt, IReadOnlyList<BookingReportRow> Items,
    int TotalCount, int Page, int PageSize, int TotalPages);

public class BookingReportQuery
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 25;
    [EnumDataType(typeof(BookingStatus))] public BookingStatus? Status { get; set; }
}

public record MaintenanceVehicleRow(Guid VehicleId, string LicensePlate, string Make, string Model,
    string HubLocation, int Mileage, VehicleStatus Status, DateTime? LastVehicleUpdateAt);
// Projection contract only: no work-order entities or lifecycle logic are created in this sprint.
public record MaintenanceHistoryRow(DateTime ChangedAt, string Status, string? Details);
public record MaintenanceRecordRow(Guid MaintenanceId, Guid VehicleId, string? LicensePlate,
    string Activity, string Status, DateTime? ScheduledAt, DateTime? CompletedAt, decimal? Cost,
    IReadOnlyList<MaintenanceHistoryRow> History);
public record MaintenanceRecordsSnapshot(bool Available, string? UnavailableReason,
    IReadOnlyList<MaintenanceRecordRow> Records);
public record MaintenanceReportResponse(DateTime GeneratedAt, IReadOnlyList<MaintenanceVehicleRow> CurrentVehicles,
    MaintenanceRecordsSnapshot WorkOrders);
public record MaintenanceStatistics(int VehiclesInMaintenance, bool WorkOrderDataAvailable,
    int? WorkOrderCount, int? CostedWorkOrderCount, decimal? RecordedCostTotal, string? UnavailableReason);
public record OperationalStatisticsResponse(DateTime GeneratedAt, FleetSummaryResponse Fleet,
    BookingStatistics Bookings, MaintenanceStatistics Maintenance);
