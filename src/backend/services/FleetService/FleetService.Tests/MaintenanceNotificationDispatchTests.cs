using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Messaging;
using FleetService.Api.Messaging.Events;
using FleetService.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FleetService.Tests;

public class MaintenanceNotificationDispatchTests
{
    private sealed class Dispatcher(FleetDbContext db, bool fail = false) : INotificationEventDispatcher
    {
        public List<NotificationDomainEvent> Events { get; } = [];

        public bool TryEnqueue(NotificationDomainEvent domainEvent)
        {
            Events.Add(domainEvent);
            var maintenanceId = domainEvent switch
            {
                MaintenanceScheduledEvent scheduled => scheduled.MaintenanceId,
                MaintenanceStatusChangedEvent changed => changed.MaintenanceId,
                _ => throw new Xunit.Sdk.XunitException($"Unexpected event {domainEvent.EventType}")
            };
            Assert.True(db.MaintenanceRecords.AsNoTracking().Any(record => record.Id == maintenanceId));
            if (fail) throw new InvalidOperationException("Dispatcher unavailable");
            return true;
        }
    }

    [Fact]
    public async Task ScheduleAndStatusChange_DispatchAfterPersistence_ToPersistedCreator()
    {
        await using var db = CreateContext();
        var vehicle = await SeedVehicleAsync(db);
        var creator = Guid.NewGuid();
        var dispatcher = new Dispatcher(db);
        var service = new MaintenanceService(db, dispatcher);

        var created = await service.CreateMaintenanceRecordAsync(creator, Request(vehicle.Id));
        await service.UpdateMaintenanceStatusAsync(created.Id, MaintenanceStatus.IN_PROGRESS);

        var scheduled = Assert.IsType<MaintenanceScheduledEvent>(dispatcher.Events[0]);
        Assert.Equal(created.Id, scheduled.MaintenanceId);
        Assert.Equal(vehicle.Id, scheduled.VehicleId);
        Assert.Equal("Brake inspection", scheduled.Activity);
        Assert.Equal([creator], scheduled.TargetUserIds);
        Assert.NotEqual(Guid.Empty, scheduled.EventId);

        var changed = Assert.IsType<MaintenanceStatusChangedEvent>(dispatcher.Events[1]);
        Assert.Equal(created.Id, changed.MaintenanceId);
        Assert.Equal("IN_PROGRESS", changed.Status);
        Assert.Equal([creator], changed.TargetUserIds);
        Assert.Equal(MaintenanceStatus.IN_PROGRESS,
            (await db.MaintenanceRecords.FindAsync(created.Id))!.Status);
    }

    [Fact]
    public async Task DispatcherFailure_DoesNotRollBackMaintenancePersistence()
    {
        await using var db = CreateContext();
        var vehicle = await SeedVehicleAsync(db);
        var creator = Guid.NewGuid();
        var service = new MaintenanceService(db, new Dispatcher(db, fail: true));

        var created = await service.CreateMaintenanceRecordAsync(creator, Request(vehicle.Id));
        var updated = await service.UpdateMaintenanceStatusAsync(created.Id, MaintenanceStatus.CANCELLED);

        Assert.Equal(MaintenanceStatus.CANCELLED, updated!.Status);
        Assert.Equal(MaintenanceStatus.CANCELLED,
            (await db.MaintenanceRecords.FindAsync(created.Id))!.Status);
        Assert.Equal(VehicleStatus.Available, (await db.Vehicles.FindAsync(vehicle.Id))!.Status);
    }

    private static FleetDbContext CreateContext() => new(new DbContextOptionsBuilder<FleetDbContext>()
        .UseInMemoryDatabase($"MaintenanceNotificationDispatchTests_{Guid.NewGuid()}").Options);

    private static async Task<Vehicle> SeedVehicleAsync(FleetDbContext db)
    {
        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(), Vin = Guid.NewGuid().ToString("N")[..17],
            LicensePlate = "WP-TEST", Make = "Toyota", Model = "Axio",
            Status = VehicleStatus.Available
        };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        return vehicle;
    }

    private static CreateMaintenanceRecordRequest Request(Guid vehicleId) => new()
    {
        VehicleId = vehicleId,
        ScheduledDateTime = DateTime.UtcNow.AddDays(1),
        ServiceInformation = "Brake inspection",
        Details = "Inspect brake pads",
        Cost = 2500m
    };
}
