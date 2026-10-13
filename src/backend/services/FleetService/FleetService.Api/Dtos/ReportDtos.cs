using System.ComponentModel.DataAnnotations;
using FleetService.Api.Entities;

namespace FleetService.Api.Dtos;

public record VehicleStatusSummary(int Total, int Available, int InUse, int Maintenance, int Retired);
public record FleetSummaryResponse(DateTime GeneratedAt, VehicleStatusSummary Vehicles,
    int UtilizationEligibleVehicles, int CurrentlyBookedVehicles, decimal? CurrentBookingUtilizationPercent);
public record MaintenanceVehicleRow(Guid VehicleId, string LicensePlate, string Make, string Model,
    string HubLocation, int Mileage, VehicleStatus Status, DateTime? LastVehicleUpdateAt);
// Optional transition history remains empty until a persisted transition-audit model is introduced.
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
