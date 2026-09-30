using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Exceptions;
using FleetService.Api.Services;
using Xunit;

namespace FleetService.Tests;

public class BookingServiceTests
{
    private FleetDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new FleetDbContext(options);
    }

    private async Task<Vehicle> SeedTestVehicleAsync(FleetDbContext context, VehicleStatus status = VehicleStatus.Available)
    {
        var category = new VehicleCategory
        {
            Id = Guid.NewGuid(),
            Name = "Executive Sedan",
            Description = "Luxury Sedan"
        };
        context.VehicleCategories.Add(category);

        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            Vin = $"VIN{Guid.NewGuid().ToString("N")[..13]}",
            LicensePlate = $"PLATE-{Guid.NewGuid().ToString("N")[..5]}",
            Make = "BMW",
            Model = "5 Series",
            Year = 2024,
            DailyRate = 150.00m,
            Transmission = "Automatic",
            FuelType = "Gasoline",
            SeatingCapacity = "5",
            HubLocation = "Downtown Hub",
            Mileage = 5000,
            Status = status,
            VehicleCategoryId = category.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        return vehicle;
    }

    [Fact]
    public async Task CreateBookingAsync_ValidRequest_CreatesAndReturnsBookingResponse()
    {
        // Arrange
        using var dbContext = GetInMemoryDbContext();
        var vehicle = await SeedTestVehicleAsync(dbContext);
        var bookingService = new BookingService(dbContext);

        var customerId = Guid.NewGuid();
        var request = new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(4)
        };

        // Act
        var result = await bookingService.CreateBookingAsync(customerId, request);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(customerId, result.CustomerId);
        Assert.Equal(vehicle.Id, result.VehicleId);
        Assert.Equal(BookingStatus.Confirmed, result.Status);
        Assert.Equal(450.00m, result.TotalCost); // 3 days * 150.00
        Assert.NotNull(result.Vehicle);
        Assert.Equal("BMW", result.Vehicle.Make);
    }

    [Fact]
    public async Task CreateBookingAsync_StartDateTimeInPast_ThrowsValidationException()
    {
        // Arrange
        using var dbContext = GetInMemoryDbContext();
        var vehicle = await SeedTestVehicleAsync(dbContext);
        var bookingService = new BookingService(dbContext);

        var customerId = Guid.NewGuid();
        var request = new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = DateTime.UtcNow.AddDays(-2),
            EndDateTime = DateTime.UtcNow.AddDays(1)
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => bookingService.CreateBookingAsync(customerId, request));
        Assert.Contains("future", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateBookingAsync_EndDateTimeBeforeStartDateTime_ThrowsValidationException()
    {
        // Arrange
        using var dbContext = GetInMemoryDbContext();
        var vehicle = await SeedTestVehicleAsync(dbContext);
        var bookingService = new BookingService(dbContext);

        var customerId = Guid.NewGuid();
        var start = DateTime.UtcNow.AddDays(2);
        var request = new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = start,
            EndDateTime = start.AddHours(-2)
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => bookingService.CreateBookingAsync(customerId, request));
        Assert.Contains("after", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateBookingAsync_VehicleNotFound_ThrowsNotFoundException()
    {
        // Arrange
        using var dbContext = GetInMemoryDbContext();
        var bookingService = new BookingService(dbContext);

        var customerId = Guid.NewGuid();
        var request = new CreateBookingRequest
        {
            VehicleId = Guid.NewGuid(),
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(2)
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => bookingService.CreateBookingAsync(customerId, request));
    }

    [Fact]
    public async Task CreateBookingAsync_VehicleNotAvailable_ThrowsValidationException()
    {
        // Arrange
        using var dbContext = GetInMemoryDbContext();
        var vehicle = await SeedTestVehicleAsync(dbContext, VehicleStatus.Maintenance);
        var bookingService = new BookingService(dbContext);

        var customerId = Guid.NewGuid();
        var request = new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(2)
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => bookingService.CreateBookingAsync(customerId, request));
        Assert.Contains("Maintenance", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateBookingAsync_OverlappingBookingExists_ThrowsDuplicateException()
    {
        // Arrange
        using var dbContext = GetInMemoryDbContext();
        var vehicle = await SeedTestVehicleAsync(dbContext);
        var bookingService = new BookingService(dbContext);

        var customer1 = Guid.NewGuid();
        var customer2 = Guid.NewGuid();

        var start = DateTime.UtcNow.AddDays(5);
        var end = DateTime.UtcNow.AddDays(10);

        // First booking: Day 5 to Day 10
        await bookingService.CreateBookingAsync(customer1, new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = start,
            EndDateTime = end
        });

        // Overlapping booking request: Day 7 to Day 12
        var overlappingRequest = new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = start.AddDays(2),
            EndDateTime = end.AddDays(2)
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DuplicateException>(() => bookingService.CreateBookingAsync(customer2, overlappingRequest));
        Assert.Contains("overlapping", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckVehicleAvailabilityAsync_NoOverlappingBooking_ReturnsTrue()
    {
        // Arrange
        using var dbContext = GetInMemoryDbContext();
        var vehicle = await SeedTestVehicleAsync(dbContext);
        var bookingService = new BookingService(dbContext);

        var start = DateTime.UtcNow.AddDays(5);
        var end = DateTime.UtcNow.AddDays(10);

        await bookingService.CreateBookingAsync(Guid.NewGuid(), new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = start,
            EndDateTime = end
        });

        // Non-overlapping range: Day 11 to Day 15
        var isAvailable = await bookingService.CheckVehicleAvailabilityAsync(vehicle.Id, start.AddDays(6), start.AddDays(10));

        // Assert
        Assert.True(isAvailable);
    }

    [Fact]
    public async Task GetCustomerBookingsAsync_ReturnsOnlyBookingsForCustomer()
    {
        // Arrange
        using var dbContext = GetInMemoryDbContext();
        var vehicle = await SeedTestVehicleAsync(dbContext);
        var bookingService = new BookingService(dbContext);

        var customer1 = Guid.NewGuid();
        var customer2 = Guid.NewGuid();

        await bookingService.CreateBookingAsync(customer1, new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(3)
        });

        await bookingService.CreateBookingAsync(customer2, new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = DateTime.UtcNow.AddDays(4),
            EndDateTime = DateTime.UtcNow.AddDays(6)
        });

        // Act
        var customer1Bookings = await bookingService.GetCustomerBookingsAsync(customer1);

        // Assert
        Assert.Single(customer1Bookings);
        Assert.Equal(customer1, customer1Bookings.First().CustomerId);
    }

    [Fact]
    public async Task CheckVehicleAvailabilityAsync_OverlappingConfirmedBooking_ReturnsFalse()
    {
        using var dbContext = GetInMemoryDbContext();
        var vehicle = await SeedTestVehicleAsync(dbContext);
        var bookingService = new BookingService(dbContext);
        var start = DateTime.UtcNow.AddDays(10);

        await bookingService.CreateBookingAsync(Guid.NewGuid(), new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = start,
            EndDateTime = start.AddDays(3)
        });

        var isAvailable = await bookingService.CheckVehicleAvailabilityAsync(
            vehicle.Id,
            start.AddDays(1),
            start.AddDays(4));

        Assert.False(isAvailable);
    }

    [Fact]
    public async Task CheckVehicleAvailabilityAsync_MissingVehicle_ThrowsNotFoundException()
    {
        using var dbContext = GetInMemoryDbContext();
        var bookingService = new BookingService(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(() => bookingService.CheckVehicleAvailabilityAsync(
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(2)));
    }

    [Fact]
    public async Task CheckVehicleAvailabilityAsync_InvalidPeriod_ThrowsValidationException()
    {
        using var dbContext = GetInMemoryDbContext();
        var vehicle = await SeedTestVehicleAsync(dbContext);
        var bookingService = new BookingService(dbContext);
        var start = DateTime.UtcNow.AddDays(2);

        await Assert.ThrowsAsync<ValidationException>(() => bookingService.CheckVehicleAvailabilityAsync(
            vehicle.Id,
            start,
            start.AddHours(-1)));
    }

    [Fact]
    public async Task CheckVehicleAvailabilityAsync_NonAvailableVehicle_ThrowsValidationException()
    {
        using var dbContext = GetInMemoryDbContext();
        var vehicle = await SeedTestVehicleAsync(dbContext, VehicleStatus.Maintenance);
        var bookingService = new BookingService(dbContext);

        await Assert.ThrowsAsync<ValidationException>(() => bookingService.CheckVehicleAvailabilityAsync(
            vehicle.Id,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(2)));
    }

    [Fact]
    public async Task CancelBookingAsync_OwnConfirmedBooking_PersistsCancellationAndReleasesPeriod()
    {
        using var dbContext = GetInMemoryDbContext();
        var vehicle = await SeedTestVehicleAsync(dbContext);
        var bookingService = new BookingService(dbContext);
        var customerId = Guid.NewGuid();
        var start = DateTime.UtcNow.AddDays(15);
        var end = start.AddDays(3);

        var created = await bookingService.CreateBookingAsync(customerId, new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = start,
            EndDateTime = end
        });

        var cancelled = await bookingService.CancelBookingAsync(created.Id, customerId);
        var persisted = await dbContext.Bookings.SingleAsync(b => b.Id == created.Id);
        var isAvailable = await bookingService.CheckVehicleAvailabilityAsync(vehicle.Id, start, end);

        Assert.NotNull(cancelled);
        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
        Assert.Equal(BookingStatus.Cancelled, persisted.Status);
        Assert.NotNull(persisted.UpdatedAt);
        Assert.True(isAvailable);
    }

    [Fact]
    public async Task CancelBookingAsync_AnotherCustomersBooking_ReturnsNullAndDoesNotChangeStatus()
    {
        using var dbContext = GetInMemoryDbContext();
        var vehicle = await SeedTestVehicleAsync(dbContext);
        var bookingService = new BookingService(dbContext);
        var ownerId = Guid.NewGuid();

        var created = await bookingService.CreateBookingAsync(ownerId, new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = DateTime.UtcNow.AddDays(20),
            EndDateTime = DateTime.UtcNow.AddDays(22)
        });

        var result = await bookingService.CancelBookingAsync(created.Id, Guid.NewGuid());
        var persisted = await dbContext.Bookings.SingleAsync(b => b.Id == created.Id);

        Assert.Null(result);
        Assert.Equal(BookingStatus.Confirmed, persisted.Status);
    }

    [Fact]
    public async Task CancelBookingAsync_AlreadyCancelled_ThrowsDuplicateException()
    {
        using var dbContext = GetInMemoryDbContext();
        var vehicle = await SeedTestVehicleAsync(dbContext);
        var bookingService = new BookingService(dbContext);
        var customerId = Guid.NewGuid();

        var created = await bookingService.CreateBookingAsync(customerId, new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = DateTime.UtcNow.AddDays(25),
            EndDateTime = DateTime.UtcNow.AddDays(27)
        });

        await bookingService.CancelBookingAsync(created.Id, customerId);

        var exception = await Assert.ThrowsAsync<DuplicateException>(() =>
            bookingService.CancelBookingAsync(created.Id, customerId));
        Assert.Contains("already", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelBookingAsync_CompletedBooking_ThrowsValidationException()
    {
        using var dbContext = GetInMemoryDbContext();
        var vehicle = await SeedTestVehicleAsync(dbContext);
        var customerId = Guid.NewGuid();
        var booking = new Booking
        {
            CustomerId = customerId,
            VehicleId = vehicle.Id,
            StartDateTime = DateTime.UtcNow.AddDays(-3),
            EndDateTime = DateTime.UtcNow.AddDays(-1),
            Status = BookingStatus.Completed,
            TotalCost = 300m
        };
        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();
        var bookingService = new BookingService(dbContext);

        await Assert.ThrowsAsync<ValidationException>(() =>
            bookingService.CancelBookingAsync(booking.Id, customerId));
    }
}
