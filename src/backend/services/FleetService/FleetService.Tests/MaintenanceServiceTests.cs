using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Exceptions;
using FleetService.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FleetService.Tests;

public class MaintenanceServiceTests
{
    [Fact]
    public async Task CreateRecord_PersistsScheduledRecordAndMakesVehicleUnavailable()
    {
        await using var context = CreateContext();
        var vehicle = await SeedVehicleAsync(context);
        var service = new MaintenanceService(context);

        var result = await service.CreateMaintenanceRecordAsync(Guid.NewGuid(), ValidRequest(vehicle.Id));

        Assert.Equal(MaintenanceStatus.SCHEDULED, result.Status);
        Assert.Equal(vehicle.Id, result.VehicleId);
        Assert.Equal("Brake inspection and service", result.ServiceInformation);
        Assert.Equal(2500m, result.Cost);
        Assert.Single(await context.MaintenanceRecords.ToListAsync());
        Assert.Equal(VehicleStatus.Maintenance, (await context.Vehicles.FindAsync(vehicle.Id))!.Status);
    }

    [Fact]
    public async Task CreateRecord_MakesVehicleUnavailableToBookingService()
    {
        await using var context = CreateContext();
        var vehicle = await SeedVehicleAsync(context);
        var maintenanceService = new MaintenanceService(context);
        await maintenanceService.CreateMaintenanceRecordAsync(Guid.NewGuid(), ValidRequest(vehicle.Id));

        var bookingService = new BookingService(context);
        var exception = await Assert.ThrowsAsync<ValidationException>(() => bookingService.CreateBookingAsync(
            Guid.NewGuid(),
            new CreateBookingRequest
            {
                VehicleId = vehicle.Id,
                StartDateTime = DateTime.UtcNow.AddDays(3),
                EndDateTime = DateTime.UtcNow.AddDays(5)
            }));

        Assert.Contains("not available", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActiveMaintenance_PreventsGenericVehicleStatusRelease()
    {
        await using var context = CreateContext();
        var vehicle = await SeedVehicleAsync(context);
        var maintenanceService = new MaintenanceService(context);
        await maintenanceService.CreateMaintenanceRecordAsync(Guid.NewGuid(), ValidRequest(vehicle.Id));
        var vehicleService = new VehicleService(context, null, null);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => vehicleService.UpdateVehicleStatusAsync(
            vehicle.Id,
            new UpdateVehicleStatusRequest { Status = VehicleStatus.Available }));

        Assert.Contains("active maintenance", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(VehicleStatus.Maintenance, (await context.Vehicles.FindAsync(vehicle.Id))!.Status);
    }

    [Fact]
    public async Task CreateRecord_WhenVehicleAlreadyHasActiveRecord_IsRejected()
    {
        await using var context = CreateContext();
        var vehicle = await SeedVehicleAsync(context);
        var service = new MaintenanceService(context);
        await service.CreateMaintenanceRecordAsync(Guid.NewGuid(), ValidRequest(vehicle.Id));

        await Assert.ThrowsAsync<DuplicateException>(() =>
            service.CreateMaintenanceRecordAsync(Guid.NewGuid(), ValidRequest(vehicle.Id)));
    }

    [Theory]
    [InlineData(VehicleStatus.InUse)]
    [InlineData(VehicleStatus.Retired)]
    public async Task CreateRecord_ForIneligibleVehicleStatus_IsRejected(VehicleStatus status)
    {
        await using var context = CreateContext();
        var vehicle = await SeedVehicleAsync(context, status);
        var service = new MaintenanceService(context);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateMaintenanceRecordAsync(Guid.NewGuid(), ValidRequest(vehicle.Id)));
    }

    [Fact]
    public async Task CreateRecord_WithInvalidRequest_IsRejected()
    {
        await using var context = CreateContext();
        var vehicle = await SeedVehicleAsync(context);
        var service = new MaintenanceService(context);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateMaintenanceRecordAsync(
            Guid.NewGuid(),
            new CreateMaintenanceRecordRequest
            {
                VehicleId = vehicle.Id,
                ScheduledDateTime = DateTime.UtcNow.AddDays(-1),
                ServiceInformation = " ",
                Cost = -1
            }));
    }

    [Fact]
    public async Task UpdateRecord_PersistsServiceDetailsAndCost()
    {
        await using var context = CreateContext();
        var vehicle = await SeedVehicleAsync(context);
        var service = new MaintenanceService(context);
        var created = await service.CreateMaintenanceRecordAsync(Guid.NewGuid(), ValidRequest(vehicle.Id));

        var updated = await service.UpdateMaintenanceRecordAsync(created.Id, new UpdateMaintenanceRecordRequest
        {
            ServiceInformation = "Brake inspection, pads and fluid replacement",
            Details = "Front pads replaced; road test completed.",
            Cost = 8750m
        });

        Assert.NotNull(updated);
        Assert.Equal(8750m, updated.Cost);
        Assert.Contains("Front pads", updated.Details);
        var persisted = await context.MaintenanceRecords.FindAsync(created.Id);
        Assert.Equal(8750m, persisted!.Cost);
        Assert.NotNull(persisted.UpdatedAt);
    }

    [Fact]
    public async Task StatusTransitions_ValidSequencePersistsHistoryAndReleasesVehicle()
    {
        await using var context = CreateContext();
        var vehicle = await SeedVehicleAsync(context);
        var service = new MaintenanceService(context);
        var created = await service.CreateMaintenanceRecordAsync(Guid.NewGuid(), ValidRequest(vehicle.Id));

        var inProgress = await service.UpdateMaintenanceStatusAsync(created.Id, MaintenanceStatus.IN_PROGRESS);
        var completed = await service.UpdateMaintenanceStatusAsync(created.Id, MaintenanceStatus.COMPLETED);
        var history = await service.GetVehicleMaintenanceHistoryAsync(vehicle.Id);

        Assert.Equal(MaintenanceStatus.IN_PROGRESS, inProgress!.Status);
        Assert.Equal(MaintenanceStatus.COMPLETED, completed!.Status);
        Assert.NotNull(completed.CompletedAt);
        Assert.Contains(history, record => record.Id == created.Id && record.Status == MaintenanceStatus.COMPLETED);
        Assert.Equal(VehicleStatus.Available, (await context.Vehicles.FindAsync(vehicle.Id))!.Status);
    }

    [Fact]
    public async Task StatusTransition_InvalidTransitionIsRejectedWithoutChangingState()
    {
        await using var context = CreateContext();
        var vehicle = await SeedVehicleAsync(context);
        var service = new MaintenanceService(context);
        var created = await service.CreateMaintenanceRecordAsync(Guid.NewGuid(), ValidRequest(vehicle.Id));

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateMaintenanceStatusAsync(created.Id, MaintenanceStatus.COMPLETED));

        Assert.Equal(MaintenanceStatus.SCHEDULED, (await context.MaintenanceRecords.FindAsync(created.Id))!.Status);
        Assert.Equal(VehicleStatus.Maintenance, (await context.Vehicles.FindAsync(vehicle.Id))!.Status);
    }

    [Fact]
    public async Task CancelledRecord_RemainsInHistoryAndReleasesVehicle()
    {
        await using var context = CreateContext();
        var vehicle = await SeedVehicleAsync(context);
        var service = new MaintenanceService(context);
        var created = await service.CreateMaintenanceRecordAsync(Guid.NewGuid(), ValidRequest(vehicle.Id));

        var cancelled = await service.UpdateMaintenanceStatusAsync(created.Id, MaintenanceStatus.CANCELLED);
        var history = await service.GetVehicleMaintenanceHistoryAsync(vehicle.Id);

        Assert.Equal(MaintenanceStatus.CANCELLED, cancelled!.Status);
        Assert.Single(history);
        Assert.Equal(MaintenanceStatus.CANCELLED, history[0].Status);
        Assert.Equal(VehicleStatus.Available, (await context.Vehicles.FindAsync(vehicle.Id))!.Status);
    }

    [Fact]
    public async Task GetVehicleHistory_ReturnsOnlySelectedVehicleInSensibleOrderAndHandlesEmptyHistory()
    {
        await using var context = CreateContext();
        var firstVehicle = await SeedVehicleAsync(context);
        var secondVehicle = await SeedVehicleAsync(context);
        var service = new MaintenanceService(context);

        var first = await service.CreateMaintenanceRecordAsync(Guid.NewGuid(), ValidRequest(firstVehicle.Id, 10));
        await service.UpdateMaintenanceStatusAsync(first.Id, MaintenanceStatus.CANCELLED);
        await service.CreateMaintenanceRecordAsync(Guid.NewGuid(), ValidRequest(firstVehicle.Id, 20));

        var firstHistory = await service.GetVehicleMaintenanceHistoryAsync(firstVehicle.Id);
        var emptyHistory = await service.GetVehicleMaintenanceHistoryAsync(secondVehicle.Id);

        Assert.Equal(2, firstHistory.Count);
        Assert.True(firstHistory[0].ScheduledDateTime > firstHistory[1].ScheduledDateTime);
        Assert.All(firstHistory, record => Assert.Equal(firstVehicle.Id, record.VehicleId));
        Assert.Empty(emptyHistory);
    }

    private static FleetDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseInMemoryDatabase($"MaintenanceServiceTests_{Guid.NewGuid()}")
            .Options;
        return new FleetDbContext(options);
    }

    private static async Task<Vehicle> SeedVehicleAsync(
        FleetDbContext context,
        VehicleStatus status = VehicleStatus.Available)
    {
        var category = new VehicleCategory
        {
            Id = Guid.NewGuid(),
            Name = $"Category-{Guid.NewGuid():N}",
            Description = "Maintenance test category"
        };
        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            Vin = Guid.NewGuid().ToString("N")[..17].ToUpperInvariant(),
            LicensePlate = $"M-{Guid.NewGuid().ToString("N")[..8]}",
            Make = "Toyota",
            Model = "Axio",
            Year = 2024,
            DailyRate = 12000m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5",
            HubLocation = "Colombo Fort Hub",
            Mileage = 42000,
            Status = status,
            VehicleCategoryId = category.Id,
            Category = category
        };
        context.AddRange(category, vehicle);
        await context.SaveChangesAsync();
        return vehicle;
    }

    private static CreateMaintenanceRecordRequest ValidRequest(Guid vehicleId, int daysAhead = 1)
    {
        return new CreateMaintenanceRecordRequest
        {
            VehicleId = vehicleId,
            ScheduledDateTime = DateTime.UtcNow.AddDays(daysAhead),
            ServiceInformation = "Brake inspection and service",
            Details = "Inspect pads, rotors and fluid.",
            Cost = 2500m
        };
    }
}
