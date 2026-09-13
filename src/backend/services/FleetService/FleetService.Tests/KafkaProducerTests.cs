using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Messaging;
using FleetService.Api.Messaging.Events;
using FleetService.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FleetService.Tests;

public class KafkaProducerTests
{
    private class MockKafkaProducer : IKafkaProducerService
    {
        public List<(string Topic, string Key, object Message)> PublishedEvents { get; } = new();

        public Task PublishAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default)
        {
            PublishedEvents.Add((topic, key, message!));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task KafkaProducerService_WhenDisabled_DoesNotThrowAndCompletes()
    {
        // Arrange
        var settings = Options.Create(new KafkaSettings
        {
            Enabled = false,
            BootstrapServers = "localhost:9092"
        });
        var logger = NullLogger<KafkaProducerService>.Instance;
        using var producer = new KafkaProducerService(settings, logger);

        var updateEvent = new VehicleUpdatedEvent
        {
            VehicleId = Guid.NewGuid(),
            Vin = "1HGCR2F83HA000001",
            LicensePlate = "WP-CAB-1001",
            Make = "Honda",
            Model = "Accord",
            DailyRate = 18500m
        };

        // Act & Assert - should execute gracefully without exception
        var exception = await Record.ExceptionAsync(() => producer.PublishAsync("fleetflow.vehicle.events", updateEvent.VehicleId.ToString(), updateEvent));
        Assert.Null(exception);
    }

    [Fact]
    public async Task KafkaProducerService_WhenUnreachableBroker_GracefullyHandlesFailureWithoutThrowing()
    {
        // Arrange
        var settings = Options.Create(new KafkaSettings
        {
            Enabled = true,
            BootstrapServers = "127.0.0.1:1" // Unreachable port
        });
        var logger = NullLogger<KafkaProducerService>.Instance;
        using var producer = new KafkaProducerService(settings, logger);

        var updateEvent = new VehicleUpdatedEvent
        {
            VehicleId = Guid.NewGuid(),
            Vin = "1HGCR2F83HA000002",
            LicensePlate = "WP-CAB-1002",
            Make = "Toyota",
            Model = "Camry",
            DailyRate = 19000m
        };

        // Act & Assert - should catch internal error and not throw to caller
        var exception = await Record.ExceptionAsync(() => producer.PublishAsync("fleetflow.vehicle.events", updateEvent.VehicleId.ToString(), updateEvent));
        Assert.Null(exception);
    }

    [Fact]
    public async Task VehicleService_CreateVehicle_PublishesVehicleCreatedEventToKafka()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var dbContext = new FleetDbContext(options);

        var category = new VehicleCategory
        {
            Id = Guid.NewGuid(),
            Name = "Executive Sedan",
            Description = "Sedans"
        };
        dbContext.VehicleCategories.Add(category);
        await dbContext.SaveChangesAsync();

        var mockKafka = new MockKafkaProducer();
        var kafkaSettings = Options.Create(new KafkaSettings
        {
            Enabled = true,
            VehicleEventsTopic = "fleetflow.vehicle.events"
        });

        var vehicleService = new VehicleService(dbContext, mockKafka, kafkaSettings);

        var createRequest = new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA000010",
            LicensePlate = "WP-CAB-2001",
            Make = "Audi",
            Model = "A6",
            Year = 2024,
            VehicleCategoryId = category.Id,
            DailyRate = 25000m,
            Transmission = "Automatic",
            FuelType = "Gasoline",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Colombo Fort Hub",
            Mileage = 5000
        };

        // Act
        var created = await vehicleService.CreateVehicleAsync(createRequest);

        // Assert
        Assert.NotNull(created);
        Assert.Single(mockKafka.PublishedEvents);

        var published = mockKafka.PublishedEvents[0];
        Assert.Equal("fleetflow.vehicle.events", published.Topic);
        Assert.Equal(created.Id.ToString(), published.Key);

        var createdEvent = Assert.IsType<VehicleCreatedEvent>(published.Message);
        Assert.Equal("1HGCR2F83HA000010", createdEvent.Vin);
        Assert.Equal("WP-CAB-2001", createdEvent.LicensePlate);
        Assert.Equal("Audi", createdEvent.Make);
        Assert.Equal("A6", createdEvent.Model);
        Assert.Equal(25000m, createdEvent.DailyRate);
    }

    [Fact]
    public async Task VehicleService_UpdateVehicle_PublishesVehicleUpdatedEventToKafka()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var dbContext = new FleetDbContext(options);

        var category = new VehicleCategory
        {
            Id = Guid.NewGuid(),
            Name = "Executive Sedan",
            Description = "Sedans"
        };
        dbContext.VehicleCategories.Add(category);

        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            Vin = "1HGCR2F83HA000020",
            LicensePlate = "WP-CAB-3001",
            Make = "Mercedes",
            Model = "C200",
            Year = 2023,
            VehicleCategoryId = category.Id,
            Category = category,
            DailyRate = 30000m,
            Transmission = "Automatic",
            FuelType = "Gasoline",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Colombo Fort Hub",
            Mileage = 8000,
            Status = VehicleStatus.Available,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Vehicles.Add(vehicle);
        await dbContext.SaveChangesAsync();

        var mockKafka = new MockKafkaProducer();
        var kafkaSettings = Options.Create(new KafkaSettings
        {
            Enabled = true,
            VehicleEventsTopic = "fleetflow.vehicle.events"
        });

        var vehicleService = new VehicleService(dbContext, mockKafka, kafkaSettings);

        var updateRequest = new UpdateVehicleRequest
        {
            Vin = vehicle.Vin,
            LicensePlate = "WP-CAB-3001-MOD",
            Make = "Mercedes",
            Model = "C300 AMG",
            Year = 2024,
            VehicleCategoryId = category.Id,
            DailyRate = 35000m,
            Transmission = "Automatic",
            FuelType = "Gasoline",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Kandy Central Station",
            Mileage = 9500
        };

        // Act
        var updated = await vehicleService.UpdateVehicleAsync(vehicle.Id, updateRequest);

        // Assert
        Assert.NotNull(updated);
        Assert.Single(mockKafka.PublishedEvents);

        var published = mockKafka.PublishedEvents[0];
        Assert.Equal("fleetflow.vehicle.events", published.Topic);
        Assert.Equal(vehicle.Id.ToString(), published.Key);

        var updatedEvent = Assert.IsType<VehicleUpdatedEvent>(published.Message);
        Assert.Equal(vehicle.Vin, updatedEvent.Vin);
        Assert.Equal("WP-CAB-3001-MOD", updatedEvent.LicensePlate);
        Assert.Equal("C300 AMG", updatedEvent.Model);
        Assert.Equal(35000m, updatedEvent.DailyRate);
        Assert.Equal("Kandy Central Station", updatedEvent.HubLocation);
        Assert.Equal(9500, updatedEvent.Mileage);
        Assert.NotNull(updatedEvent.UpdatedAt);
    }
}
