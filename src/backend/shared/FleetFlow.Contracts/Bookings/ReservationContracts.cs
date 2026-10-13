using FleetService.Api.Dtos;
namespace FleetFlow.Contracts;
public sealed record ReserveVehicleCommand(Guid BookingId, Guid VehicleId, Guid CustomerId, DateTime StartDateTime, DateTime EndDateTime);
public sealed record ReservationAcknowledgement(Guid BookingId, bool Released, decimal TotalCost, VehicleResponse Vehicle);
public sealed record RentalStatisticsResponse(BookingStatistics Bookings, IReadOnlyList<Guid> CurrentlyBookedVehicleIds);
public static class RentalPeriod
{
    // PostgreSQL persists microseconds. Canonicalize before comparing idempotent requests.
    public static DateTime Utc(DateTime value)
    {
        var utc=value.Kind switch {DateTimeKind.Utc=>value,DateTimeKind.Local=>value.ToUniversalTime(),_=>DateTime.SpecifyKind(value,DateTimeKind.Utc)};
        return new DateTime(utc.Ticks-utc.Ticks%10,DateTimeKind.Utc);
    }
    public static string? Error(DateTime start,DateTime end,bool validateFuture=true)=>
        start==default||end==default?"Start date and time and end date and time are required.":
        validateFuture&&start<DateTime.UtcNow.AddMinutes(-5)?"Start date and time must be in the future.":
        end<=start?"End date and time must be after start date and time.":null;
    public static decimal Estimate(DateTime start,DateTime end,decimal rate)=>Math.Round((decimal)Math.Max(1,Math.Ceiling(Math.Round((end-start).TotalHours,4)/24.0))*rate,2);
}
