using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace FleetService.Api.Services;

public class ReportingService(FleetDbContext db, IMaintenanceReportSource maintenanceSource, TimeProvider clock) : IReportingService
{
    public Task<FleetSummaryResponse> GetFleetSummaryAsync(CancellationToken ct = default) =>
        ReadFleetAsync(clock.GetUtcNow().UtcDateTime, ct);

    private async Task<FleetSummaryResponse> ReadFleetAsync(DateTime now, CancellationToken ct)
    {
        var counts = await db.Vehicles.AsNoTracking().GroupBy(v => v.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        int Count(VehicleStatus status) => counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0;
        var vehicles = new VehicleStatusSummary(counts.Sum(c => c.Count), Count(VehicleStatus.Available),
            Count(VehicleStatus.InUse), Count(VehicleStatus.Maintenance), Count(VehicleStatus.Retired));
        var eligible = vehicles.Total - vehicles.Retired;
        var booked = await db.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.Confirmed && b.StartDateTime <= now && b.EndDateTime > now &&
                db.Vehicles.Any(v => v.Id == b.VehicleId && v.Status != VehicleStatus.Retired))
            .Select(b => b.VehicleId).Distinct().CountAsync(ct);
        return new FleetSummaryResponse(now, vehicles, eligible, booked,
            eligible == 0 ? null : Math.Round(booked * 100m / eligible, 2));
    }

    public async Task<BookingReportResponse> GetBookingReportAsync(BookingReportQuery query, CancellationToken ct = default)
    {
        // Validate here as well as at the API boundary; avoid integer overflow in Skip.
        if (query.Page < 1 || query.PageSize is < 1 or > 100 ||
            (long)(query.Page - 1) * query.PageSize > int.MaxValue ||
            query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
            throw new ArgumentException("Invalid report page, page size or booking status.");
        var bookings = db.Bookings.AsNoTracking().AsQueryable();
        if (query.Status.HasValue) bookings = bookings.Where(b => b.Status == query.Status.Value);
        var count = await bookings.CountAsync(ct);
        var items = await bookings.OrderByDescending(b => b.CreatedAt).ThenBy(b => b.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(b => new BookingReportRow(b.Id, b.CustomerId, b.VehicleId,
                b.Vehicle == null ? null : b.Vehicle.LicensePlate,
                b.Vehicle == null ? null : b.Vehicle.Make, b.Vehicle == null ? null : b.Vehicle.Model,
                b.Status, b.StartDateTime, b.EndDateTime, b.TotalCost, b.CreatedAt, b.UpdatedAt)).ToListAsync(ct);
        return new BookingReportResponse(clock.GetUtcNow().UtcDateTime, items, count, query.Page, query.PageSize,
            (int)Math.Ceiling(count / (decimal)query.PageSize));
    }

    public async Task<MaintenanceReportResponse> GetMaintenanceReportAsync(CancellationToken ct = default)
    {
        var vehicles = await db.Vehicles.AsNoTracking().Where(v => v.Status == VehicleStatus.Maintenance)
            .OrderBy(v => v.LicensePlate).ThenBy(v => v.Id)
            .Select(v => new MaintenanceVehicleRow(v.Id, v.LicensePlate, v.Make, v.Model, v.HubLocation,
                v.Mileage, v.Status, v.UpdatedAt)).ToListAsync(ct);
        return new MaintenanceReportResponse(clock.GetUtcNow().UtcDateTime, vehicles, await maintenanceSource.ReadAsync(ct));
    }

    public async Task<OperationalStatisticsResponse> GetOperationalStatisticsAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var fleet = await ReadFleetAsync(now, ct);
        var groups = await db.Bookings.AsNoTracking().GroupBy(b => b.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Value = g.Sum(b => b.TotalCost) }).ToListAsync(ct);
        int Count(BookingStatus status) => groups.FirstOrDefault(g => g.Status == status)?.Count ?? 0;
        var pending = Count(BookingStatus.Pending);
        var confirmed = Count(BookingStatus.Confirmed);
        var bookings = new BookingStatistics(groups.Sum(g => g.Count), pending, confirmed,
            Count(BookingStatus.Cancelled), Count(BookingStatus.Completed), pending + confirmed,
            groups.Where(g => g.Status != BookingStatus.Cancelled).Sum(g => g.Value));
        var workOrders = await maintenanceSource.ReadAsync(ct);
        var costed = workOrders.Records.Where(r => r.Cost.HasValue).ToList();
        var maintenance = new MaintenanceStatistics(fleet.Vehicles.Maintenance, workOrders.Available,
            workOrders.Available ? workOrders.Records.Count : null,
            workOrders.Available ? costed.Count : null,
            workOrders.Available ? costed.Sum(r => r.Cost!.Value) : null, workOrders.UnavailableReason);
        return new OperationalStatisticsResponse(now, fleet, bookings, maintenance);
    }
}
