using System.ComponentModel.DataAnnotations;
using FleetService.Api.Entities;

namespace FleetService.Api.Dtos;

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

public record BookingExportResponse(DateTime GeneratedAt, IReadOnlyList<BookingReportRow> Items);
public sealed class BookingExportQuery
{
    public const int MaxRows = 10000;
    [EnumDataType(typeof(BookingStatus))] public BookingStatus? Status { get; set; }
}
