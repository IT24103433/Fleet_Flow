using FleetFlow.Contracts;
using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Exceptions;
using FleetService.Api.Services;
using Microsoft.EntityFrameworkCore;
namespace FleetService.Tests;
public sealed class MaintenancePlanningTests
{
    private static FleetDbContext Db() => new(new DbContextOptionsBuilder<FleetDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task<Vehicle> Seed(FleetDbContext db)
    {
        var category = new VehicleCategory { Id = Guid.NewGuid(), Name = "Sedan" }; db.VehicleCategories.Add(category);
        var vehicle = new Vehicle { Id = Guid.NewGuid(), Vin = "MAINTTEST123456789", LicensePlate = "QA-M", Make = "Toyota", Model = "Corolla", Status = VehicleStatus.Available, DailyRate = 9000, Category = category, VehicleCategoryId = category.Id };
        db.Vehicles.Add(vehicle); await db.SaveChangesAsync(); return vehicle;
    }
    private static CreateMaintenanceRecordRequest Window(Guid id, DateTime start, DateTime? end) => new() { VehicleId = id, ScheduledDateTime = start, PlannedEndDateTime = end, ServiceInformation = "Inspect brakes", Cost = 1000 };
    private sealed class Staff(params MaintenanceStaffAccount[] users) : IMaintenanceStaffDirectory
    {
        public bool Down;
        public Task<IReadOnlyList<MaintenanceStaffAccount>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<MaintenanceStaffAccount>>(users);
        public Task<MaintenanceStaffAccount?> GetAsync(Guid id, CancellationToken ct) => Down ? throw new MaintenanceStaffUnavailableException() : Task.FromResult(users.SingleOrDefault(u => u.Id == id));
    }
    [Fact] public async Task PlannedWindowsKeepVehicleAvailableAndAcceptTouchingBookingBoundaries()
    {
        await using var db = Db(); var vehicle = await Seed(db); var start = DateTime.UtcNow.AddDays(1); var end = start.AddHours(2);
        db.Bookings.Add(new() { Id = Guid.NewGuid(), VehicleId = vehicle.Id, CustomerId = Guid.NewGuid(), StartDateTime = end, EndDateTime = end.AddHours(2), Status = BookingStatus.Confirmed }); await db.SaveChangesAsync();
        var response = await new MaintenanceService(db).CreateMaintenanceRecordAsync(Guid.NewGuid(), Window(vehicle.Id, start, end));
        Assert.Equal(VehicleStatus.Available, vehicle.Status); Assert.Equal(end, response.PlannedEndDateTime);
        Assert.True(await MaintenanceAvailability.Blocking(db, vehicle.Id, start, end).AnyAsync());
        Assert.False(await MaintenanceAvailability.Blocking(db, vehicle.Id, end, end.AddHours(1)).AnyAsync());
        Assert.Single(await db.MaintenanceAuditEntries.ToListAsync());
    }
    [Fact] public async Task OverlappingBookingsAndMaintenanceAreRejectedWithoutPartialWrites()
    {
        await using var db = Db(); var vehicle = await Seed(db); var start = DateTime.UtcNow.AddDays(1); var service = new MaintenanceService(db);
        await service.CreateMaintenanceRecordAsync(Guid.NewGuid(), Window(vehicle.Id, start, start.AddHours(2)));
        await Assert.ThrowsAsync<DuplicateException>(() => service.CreateMaintenanceRecordAsync(Guid.NewGuid(), Window(vehicle.Id, start.AddHours(1), start.AddHours(3))));
        Assert.Single(db.MaintenanceRecords); Assert.Single(db.MaintenanceAuditEntries);
        await service.CreateMaintenanceRecordAsync(Guid.NewGuid(), Window(vehicle.Id, start.AddHours(2), start.AddHours(4)));
        await Assert.ThrowsAsync<DuplicateException>(() => new BookingService(db).CreateBookingAsync(Guid.NewGuid(), new() { VehicleId = vehicle.Id, StartDateTime = start, EndDateTime = start.AddMinutes(30) }));
        Assert.Empty(db.Bookings);
    }
    [Fact] public async Task UnknownLegacyEndsAndActualWorkRemainConservative()
    {
        await using var db = Db(); var vehicle = await Seed(db); var service = new MaintenanceService(db);
        var legacy = await service.CreateMaintenanceRecordAsync(Guid.NewGuid(), Window(vehicle.Id, DateTime.UtcNow.AddDays(1), null));
        Assert.Equal(VehicleStatus.Maintenance, vehicle.Status); Assert.True(await MaintenanceAvailability.Blocking(db, vehicle.Id, DateTime.UtcNow.AddDays(10), DateTime.UtcNow.AddDays(11)).AnyAsync());
        await service.UpdateMaintenanceStatusAsync(legacy.Id, MaintenanceStatus.IN_PROGRESS);
        Assert.True(await MaintenanceAvailability.CurrentAsync(db, vehicle.Id));
        await service.UpdateMaintenanceStatusAsync(legacy.Id, MaintenanceStatus.COMPLETED);
        Assert.Equal(VehicleStatus.Available, vehicle.Status);
    }
    [Fact] public async Task AssignmentValidatesStaffAndPreservesCreatorAndImmutableActorHistory()
    {
        await using var db = Db(); var vehicle = await Seed(db); var creator = Guid.NewGuid(); var manager = Guid.NewGuid();
        var first = new MaintenanceStaffAccount(Guid.NewGuid(), "First technician", "first"); var next = new MaintenanceStaffAccount(Guid.NewGuid(), "", "next"); var directory = new Staff(first, next);
        var service = new MaintenanceService(db, staff: directory); var record = await service.CreateMaintenanceRecordAsync(creator, Window(vehicle.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2)));
        await service.AssignAsync(record.Id, manager, new() { AssignedStaffId = first.Id });
        var assigned = await service.AssignAsync(record.Id, manager, new() { AssignedStaffId = next.Id });
        Assert.Equal(creator, assigned!.CreatedByUserId); Assert.Equal(next.Id, assigned.AssignedStaffId); Assert.Equal("next", assigned.AssignedStaffName);
        var audit = (await service.AuditAsync(record.Id))!; Assert.Equal(3, audit.Count); Assert.Equal(manager, audit.Last().ActorId); Assert.Contains("First technician", audit.Last().Before);
        var report = Assert.Single((await new MaintenanceReportSource(db).ReadAsync()).Records);
        Assert.Equal(3, report.History.Count); Assert.Equal("next", report.AssignedStaffName); Assert.All(report.History, entry => Assert.Equal("SCHEDULED", entry.Status));
        var entry = await db.MaintenanceAuditEntries.FirstAsync(); entry.Action = "Changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
    [Fact] public async Task InvalidStaffOrIdentityOutageNeverChangesTheAssignment()
    {
        await using var db = Db(); var vehicle = await Seed(db); var directory = new Staff(); var service = new MaintenanceService(db, staff: directory);
        var record = await service.CreateMaintenanceRecordAsync(Guid.NewGuid(), Window(vehicle.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2)));
        await Assert.ThrowsAsync<ValidationException>(() => service.AssignAsync(record.Id, Guid.NewGuid(), new() { AssignedStaffId = Guid.NewGuid() }));
        directory.Down = true; await Assert.ThrowsAsync<MaintenanceStaffUnavailableException>(() => service.AssignAsync(record.Id, Guid.NewGuid(), new() { AssignedStaffId = Guid.NewGuid() }));
        Assert.Null((await db.MaintenanceRecords.SingleAsync()).AssignedStaffId); Assert.Single(db.MaintenanceAuditEntries);
    }
    [Fact] public async Task ScheduleChangesValidateConflictsAndRecordBeforeAndAfter()
    {
        await using var db = Db(); var vehicle = await Seed(db); var service = new MaintenanceService(db); var start = DateTime.UtcNow.AddDays(1); var actor = Guid.NewGuid();
        var legacy = await service.CreateMaintenanceRecordAsync(actor, Window(vehicle.Id, start, null));
        await service.ScheduleAsync(legacy.Id, actor, new() { ScheduledDateTime = start, PlannedEndDateTime = start.AddHours(2) });
        Assert.Equal(VehicleStatus.Available, vehicle.Status);
        await Assert.ThrowsAsync<ValidationException>(() => service.ScheduleAsync(legacy.Id, actor, new() { ScheduledDateTime = start, PlannedEndDateTime = start }));
        Assert.Equal(2, db.MaintenanceAuditEntries.Count()); Assert.Null(await service.AuditAsync(Guid.NewGuid()));
    }
    [Fact] public async Task PlannedWorkCannotStartEarlyAndTerminalWorkCannotBeReassigned()
    {
        await using var db = Db(); var vehicle = await Seed(db); var service = new MaintenanceService(db); var start = DateTime.UtcNow.AddDays(1); var actor = Guid.NewGuid();
        var record = await service.CreateMaintenanceRecordAsync(actor, Window(vehicle.Id, start, start.AddHours(2)));
        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateMaintenanceStatusAsync(record.Id, actor, MaintenanceStatus.IN_PROGRESS));
        await service.UpdateMaintenanceStatusAsync(record.Id, actor, MaintenanceStatus.CANCELLED);
        await Assert.ThrowsAsync<ValidationException>(() => service.AssignAsync(record.Id, actor, new() { AssignedStaffId = null }));
        Assert.Equal(2, db.MaintenanceAuditEntries.Count());
    }
}
