using FleetFlow.Contracts;
using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Exceptions;
using FleetService.Api.Services;
using Microsoft.EntityFrameworkCore;
namespace RentalService.Tests;
public sealed class MaintenanceWindowRaceTests
{
    [RentalPostgresFact] public async Task BookingAndMaintenanceRaceHasExactlyOneWinnerUnderTheVehicleLock()
    {
        await using var f = await RentalPostgresFixture.Create(); var start = DateTime.UtcNow.AddDays(1); var end = start.AddHours(2);
        async Task<bool> Booking() { await using var db = f.Fleet(); try { await new ReservationService(db, new VehicleService(db)).ReserveAsync(new(Guid.NewGuid(), f.VehicleId, f.Customer, start, end)); return true; } catch (DuplicateException) { return false; } }
        async Task<bool> Maintenance() { await using var db = f.Fleet(); try { await new MaintenanceService(db).CreateMaintenanceRecordAsync(Guid.NewGuid(), new() { VehicleId = f.VehicleId, ScheduledDateTime = start, PlannedEndDateTime = end, ServiceInformation = "Brake inspection" }); return true; } catch (DuplicateException) { return false; } }
        var results = await Task.WhenAll(Booking(), Maintenance()); Assert.Single(results, value => value);
        await using var check = f.Fleet(); Assert.Equal(1, await check.VehicleReservations.CountAsync() + await check.MaintenanceRecords.CountAsync());
    }
    [RentalPostgresFact] public async Task PlannedWindowBoundariesUseTheLedgerAndAuditDatabaseRejectsEdits()
    {
        await using var f = await RentalPostgresFixture.Create(); await using var db = f.Fleet(); await MaintenancePlanningSchema.EnsureAsync(db);
        var start = DateTime.UtcNow.AddDays(1); var end = start.AddHours(2); var actor = Guid.NewGuid(); var service = new MaintenanceService(db);
        var record = await service.CreateMaintenanceRecordAsync(actor, new() { VehicleId = f.VehicleId, ScheduledDateTime = start, PlannedEndDateTime = end, ServiceInformation = "Brake inspection" });
        var reservations = new ReservationService(db, new VehicleService(db));
        await reservations.ReserveAsync(new(Guid.NewGuid(), f.VehicleId, f.Customer, end, end.AddHours(2)));
        Assert.False(await reservations.AvailableAsync(f.VehicleId, start, end));
        await Assert.ThrowsAsync<DuplicateException>(() => reservations.ReserveAsync(new(Guid.NewGuid(), f.VehicleId, f.Customer, start, end)));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "MaintenanceAuditEntries" SET "Action"='Forged' WHERE "MaintenanceRecordId"={record.Id}"""));
        Assert.Equal(actor, (await db.MaintenanceAuditEntries.AsNoTracking().SingleAsync()).ActorId);
    }
    [RentalPostgresFact] public async Task AuditFailureRollsBackTheWorkOrderAndOutboxTogether()
    {
        await using var f = await RentalPostgresFixture.Create(); await using var db = f.Fleet();
        await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "MaintenanceAuditEntries" ADD CONSTRAINT audit_fail CHECK ("Action" <> 'Created')""");
        await Assert.ThrowsAsync<DbUpdateException>(() => new MaintenanceService(db).CreateMaintenanceRecordAsync(Guid.NewGuid(), new() { VehicleId = f.VehicleId, ScheduledDateTime = DateTime.UtcNow.AddDays(1), PlannedEndDateTime = DateTime.UtcNow.AddDays(2), ServiceInformation = "Brake inspection" }));
        db.ChangeTracker.Clear(); Assert.Empty(await db.MaintenanceRecords.ToListAsync()); Assert.Empty(await db.NotificationOutbox.ToListAsync());
    }
}
