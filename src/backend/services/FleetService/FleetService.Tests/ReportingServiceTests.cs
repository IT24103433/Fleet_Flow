using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FleetService.Tests;

public class ReportingServiceTests
{
    private static readonly DateTime Now = new(2026, 9, 29, 8, 0, 0, DateTimeKind.Utc);
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Now); }
    private sealed class Source(MaintenanceRecordsSnapshot snapshot) : IMaintenanceReportSource
    {
        public Task<MaintenanceRecordsSnapshot> ReadAsync(CancellationToken ct = default) => Task.FromResult(snapshot);
    }
    private static DbContextOptions<FleetDbContext> Options() => new DbContextOptionsBuilder<FleetDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
    private static ReportingService Reports(FleetDbContext db, IMaintenanceReportSource? source = null) =>
        new(db, source ?? new UnavailableMaintenanceReportSource(), new Clock());
    private static Vehicle Vehicle(VehicleStatus status = VehicleStatus.Available) => new()
    {
        Id = Guid.NewGuid(), LicensePlate = Guid.NewGuid().ToString("N")[..10], Vin = Guid.NewGuid().ToString("N")[..17],
        Make = "Test", Model = "Vehicle", Status = status, HubLocation = "Colombo", CreatedAt = Now.AddDays(-10)
    };
    private static Booking Booking(Vehicle v, BookingStatus status = BookingStatus.Confirmed,
        DateTime? start = null, DateTime? end = null, decimal cost = 100) => new()
    {
        Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), VehicleId = v.Id, Status = status,
        StartDateTime = start ?? Now.AddHours(-1), EndDateTime = end ?? Now.AddHours(1),
        TotalCost = cost, CreatedAt = Now.AddDays(-1)
    };

    [Fact]
    public async Task EmptyDatabase_ReturnsMeasuredZerosAndUnavailableMaintenanceMetrics()
    {
        using var db = new FleetDbContext(Options());
        var report = await Reports(db).GetOperationalStatisticsAsync();
        Assert.Equal(Now, report.GeneratedAt);
        Assert.Equal(0, report.Fleet.Vehicles.Total);
        Assert.Equal(0, report.Fleet.CurrentlyBookedVehicles);
        Assert.Null(report.Fleet.CurrentBookingUtilizationPercent);
        Assert.Equal(0, report.Bookings.Total);
        Assert.Equal(0m, report.Bookings.NonCancelledBookingValue);
        Assert.False(report.Maintenance.WorkOrderDataAvailable);
        Assert.Null(report.Maintenance.WorkOrderCount);
        Assert.Null(report.Maintenance.RecordedCostTotal);
        Assert.Equal(0, report.Maintenance.VehiclesInMaintenance);
        var bookings = await Reports(db).GetBookingReportAsync(new());
        Assert.Empty(bookings.Items);
        Assert.Equal(0, bookings.TotalCount);
        Assert.Equal(0, bookings.TotalPages);
        Assert.Empty((await Reports(db).GetMaintenanceReportAsync()).CurrentVehicles);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(13)]
    public async Task CountsFollowPersistedRowsAcrossContexts(int count)
    {
        var options = Options();
        using (var writer = new FleetDbContext(options))
        {
            writer.Vehicles.AddRange(Enumerable.Range(0, count).Select(_ => Vehicle()));
            await writer.SaveChangesAsync();
        }
        using var reader = new FleetDbContext(options);
        var summary = await Reports(reader).GetFleetSummaryAsync();
        Assert.Equal(count, summary.Vehicles.Total);
        Assert.Equal(count, summary.Vehicles.Available);
        Assert.Equal(0m, summary.CurrentBookingUtilizationPercent);
        Assert.Empty(reader.ChangeTracker.Entries());
    }

    [Fact]
    public async Task FleetStatusAndCurrentUtilization_CountDistinctVehiclesAndExcludeRetired()
    {
        using var db = new FleetDbContext(Options());
        var available = Vehicle();
        var inUse = Vehicle(VehicleStatus.InUse);
        var maintenance = Vehicle(VehicleStatus.Maintenance);
        var retired = Vehicle(VehicleStatus.Retired);
        db.Vehicles.AddRange(available, inUse, maintenance, retired);
        db.Bookings.AddRange(Booking(available), Booking(available), Booking(retired),
            Booking(inUse, BookingStatus.Cancelled), Booking(maintenance, BookingStatus.Pending),
            Booking(inUse, BookingStatus.Completed), Booking(inUse, start: Now.AddDays(1), end: Now.AddDays(2)));
        await db.SaveChangesAsync();
        var summary = await Reports(db).GetFleetSummaryAsync();
        Assert.Equal(new VehicleStatusSummary(4, 1, 1, 1, 1), summary.Vehicles);
        Assert.Equal(3, summary.UtilizationEligibleVehicles);
        Assert.Equal(1, summary.CurrentlyBookedVehicles);
        Assert.Equal(33.33m, summary.CurrentBookingUtilizationPercent);
    }

    [Fact]
    public async Task Utilization_IncludesStartAndExcludesEndBoundary()
    {
        using var db = new FleetDbContext(Options());
        var startsNow = Vehicle();
        var endsNow = Vehicle();
        db.Vehicles.AddRange(startsNow, endsNow);
        db.Bookings.AddRange(Booking(startsNow, start: Now), Booking(endsNow, end: Now));
        await db.SaveChangesAsync();
        var summary = await Reports(db).GetFleetSummaryAsync();
        Assert.Equal(1, summary.CurrentlyBookedVehicles);
        Assert.Equal(50m, summary.CurrentBookingUtilizationPercent);
    }

    [Fact]
    public async Task RetiredOnlyFleet_HasNoUtilizationDenominator()
    {
        using var db = new FleetDbContext(Options());
        var retired = Vehicle(VehicleStatus.Retired);
        db.Vehicles.Add(retired);
        db.Bookings.Add(Booking(retired));
        await db.SaveChangesAsync();
        var summary = await Reports(db).GetFleetSummaryAsync();
        Assert.Equal(1, summary.Vehicles.Total);
        Assert.Equal(0, summary.UtilizationEligibleVehicles);
        Assert.Equal(0, summary.CurrentlyBookedVehicles);
        Assert.Null(summary.CurrentBookingUtilizationPercent);
    }

    [Fact]
    public async Task BookingStatistics_PreserveStatusCountsAndExcludeCancelledValue()
    {
        using var db = new FleetDbContext(Options());
        var vehicle = Vehicle();
        db.Vehicles.Add(vehicle);
        db.Bookings.AddRange(Booking(vehicle, BookingStatus.Pending, cost: 10),
            Booking(vehicle, cost: 20), Booking(vehicle, BookingStatus.Cancelled, cost: 999),
            Booking(vehicle, BookingStatus.Completed, cost: 30));
        await db.SaveChangesAsync();
        var report = await Reports(db).GetOperationalStatisticsAsync();
        Assert.Equal(new BookingStatistics(4, 1, 1, 1, 1, 2, 60), report.Bookings);
        var rows = await Reports(db).GetBookingReportAsync(new());
        Assert.Equal(4, rows.TotalCount);
        Assert.Equal(4, rows.Items.Select(b => b.Status).Distinct().Count());
        Assert.All(rows.Items, row => { Assert.Equal(vehicle.LicensePlate, row.LicensePlate); Assert.Equal(vehicle.Id, row.VehicleId); Assert.NotEqual(Guid.Empty, row.CustomerId); });
        Assert.Contains(rows.Items, b => b.Status == BookingStatus.Cancelled && b.TotalCost == 999);
    }

    [Fact]
    public async Task BookingReport_FilterAndPaginationUseStablePersistedReferences()
    {
        using var db = new FleetDbContext(Options());
        var vehicle = Vehicle();
        db.Vehicles.Add(vehicle);
        db.Bookings.AddRange(Booking(vehicle, BookingStatus.Cancelled), Booking(vehicle, BookingStatus.Cancelled), Booking(vehicle));
        await db.SaveChangesAsync();
        var first = await Reports(db).GetBookingReportAsync(new() { Status = BookingStatus.Cancelled, PageSize = 1 });
        var second = await Reports(db).GetBookingReportAsync(new() { Status = BookingStatus.Cancelled, PageSize = 1, Page = 2 });
        Assert.Equal(2, first.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(BookingStatus.Cancelled, Assert.Single(first.Items).Status);
        Assert.NotEqual(first.Items[0].BookingId, Assert.Single(second.Items).BookingId);
        Assert.Empty((await Reports(db).GetBookingReportAsync(new() { Page = 100 })).Items);
    }

    [Fact]
    public async Task RefreshReflectsNewPersistedRowsAndStatusChanges()
    {
        var options = Options();
        using var reader = new FleetDbContext(options);
        var service = Reports(reader);
        Assert.Equal(0, (await service.GetOperationalStatisticsAsync()).Bookings.Total);
        Guid bookingId;
        using (var writer = new FleetDbContext(options))
        {
            var vehicle = Vehicle();
            var booking = Booking(vehicle);
            bookingId = booking.Id;
            writer.Vehicles.Add(vehicle);
            writer.Bookings.Add(booking);
            await writer.SaveChangesAsync();
        }
        Assert.Equal(1, (await service.GetOperationalStatisticsAsync()).Fleet.CurrentlyBookedVehicles);
        using (var writer = new FleetDbContext(options))
        {
            (await writer.Bookings.SingleAsync(b => b.Id == bookingId)).Status = BookingStatus.Cancelled;
            (await writer.Vehicles.SingleAsync()).Status = VehicleStatus.Maintenance;
            await writer.SaveChangesAsync();
        }
        var refreshed = await service.GetOperationalStatisticsAsync();
        Assert.Equal(1, refreshed.Fleet.Vehicles.Total);
        Assert.Equal(1, refreshed.Bookings.Cancelled);
        Assert.Equal(0, refreshed.Fleet.CurrentlyBookedVehicles);
        Assert.Equal(1, refreshed.Maintenance.VehiclesInMaintenance);
        Assert.Empty(reader.ChangeTracker.Entries());
    }

    [Fact]
    public async Task MaintenanceReport_UsesCurrentVehicleRowsWithoutInventingActivityHistory()
    {
        using var db = new FleetDbContext(Options());
        var maintenance = Vehicle(VehicleStatus.Maintenance);
        maintenance.UpdatedAt = Now.AddDays(-2);
        maintenance.Mileage = 52341;
        db.Vehicles.AddRange(maintenance, Vehicle());
        await db.SaveChangesAsync();
        var report = await Reports(db).GetMaintenanceReportAsync();
        var row = Assert.Single(report.CurrentVehicles);
        Assert.Equal(maintenance.Id, row.VehicleId);
        Assert.Equal(maintenance.LicensePlate, row.LicensePlate);
        Assert.Equal(52341, row.Mileage);
        Assert.Equal(maintenance.UpdatedAt, row.LastVehicleUpdateAt);
        Assert.False(report.WorkOrders.Available);
        Assert.Empty(report.WorkOrders.Records);
        Assert.False(string.IsNullOrWhiteSpace(report.WorkOrders.UnavailableReason));
    }

    [Fact]
    public async Task FutureMaintenanceSource_ProjectsHistoryAndCostsAndCountsOnlyRecordedCosts()
    {
        using var db = new FleetDbContext(Options());
        var history = new[] { new MaintenanceHistoryRow(Now, "Completed", "Persisted completion details") };
        var records = new[] {
            new MaintenanceRecordRow(Guid.NewGuid(), Guid.NewGuid(), "WP-100", "Oil change", "Completed", Now.AddDays(-2), Now, 75.25m, history),
            new MaintenanceRecordRow(Guid.NewGuid(), Guid.NewGuid(), "WP-200", "Inspection", "Scheduled", Now, null, null, []) };
        var service = Reports(db, new Source(new(true, null, records)));
        var report = await service.GetMaintenanceReportAsync();
        Assert.Equal(records, report.WorkOrders.Records);
        Assert.Equal(history, report.WorkOrders.Records[0].History);
        var stats = (await service.GetOperationalStatisticsAsync()).Maintenance;
        Assert.True(stats.WorkOrderDataAvailable);
        Assert.Equal(2, stats.WorkOrderCount);
        Assert.Equal(1, stats.CostedWorkOrderCount);
        Assert.Equal(75.25m, stats.RecordedCostTotal);
    }

    [Fact]
    public async Task AvailableEmptyMaintenanceSource_HasMeasuredZeroInsteadOfUnavailable()
    {
        using var db = new FleetDbContext(Options());
        var stats = (await Reports(db, new Source(new(true, null, []))).GetOperationalStatisticsAsync()).Maintenance;
        Assert.Equal(0, stats.WorkOrderCount);
        Assert.Equal(0m, stats.RecordedCostTotal);
        Assert.True(stats.WorkOrderDataAvailable);
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public async Task InvalidPagination_IsRejectedWithoutQueries(int page, int size)
    {
        using var db = new FleetDbContext(Options());
        await Assert.ThrowsAsync<ArgumentException>(() => Reports(db).GetBookingReportAsync(new() { Page = page, PageSize = size }));
    }

    [Fact]
    public async Task UndefinedBookingStatus_IsRejected()
    {
        using var db = new FleetDbContext(Options());
        await Assert.ThrowsAsync<ArgumentException>(() => Reports(db).GetBookingReportAsync(new() { Status = (BookingStatus)99 }));
    }
}
