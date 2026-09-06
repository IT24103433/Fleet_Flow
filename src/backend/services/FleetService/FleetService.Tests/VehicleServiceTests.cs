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

public class VehicleServiceTests
{
    private static FleetDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new FleetDbContext(options);
    }

    private static async Task<VehicleCategory> SeedCategoryAsync(FleetDbContext context, string name = "Executive Sedan")
    {
        var category = new VehicleCategory
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = $"{name} description"
        };
        context.VehicleCategories.Add(category);
        await context.SaveChangesAsync();
        return category;
    }

    [Fact]
    public async Task CreateVehicleAsync_ValidRequest_CreatesVehicleAndReturnsResponse()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var category = await SeedCategoryAsync(context, "Executive Sedan");
        var service = new VehicleService(context);

        var request = new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029184",
            LicensePlate = "FL-902-XP",
            Make = "Aero",
            Model = "Apex Executive Sedan",
            Year = 2025,
            VehicleCategoryId = category.Id,
            DailyRate = 85.00m,
            Transmission = "9-Speed Automatic",
            FuelType = "Plug-in Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Metro Hub - Terminal A",
            Mileage = 12450
        };

        // Act
        var result = await service.CreateVehicleAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("1HGCR2F83HA029184", result.Vin);
        Assert.Equal("FL-902-XP", result.LicensePlate);
        Assert.Equal("Aero", result.Make);
        Assert.Equal("Apex Executive Sedan", result.Model);
        Assert.Equal(2025, result.Year);
        Assert.Equal(85.00m, result.DailyRate);
        Assert.Equal("Executive Sedan", result.CategoryName);
        Assert.Equal(category.Id, result.VehicleCategoryId);
        Assert.Equal(VehicleStatus.Available, result.Status);

        var dbVehicle = await context.Vehicles.FindAsync(result.Id);
        Assert.NotNull(dbVehicle);
        Assert.Equal("1HGCR2F83HA029184", dbVehicle.Vin);
    }

    [Fact]
    public async Task CreateVehicleAsync_DuplicateVin_ThrowsDuplicateException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var category = await SeedCategoryAsync(context, "Executive Sedan");
        var service = new VehicleService(context);

        var request1 = new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029184",
            LicensePlate = "FL-902-XP",
            Make = "Aero",
            Model = "Apex Executive Sedan",
            Year = 2025,
            VehicleCategoryId = category.Id,
            DailyRate = 85.00m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Metro Hub",
            Mileage = 1000
        };

        await service.CreateVehicleAsync(request1);

        var request2 = new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029184", // duplicate VIN
            LicensePlate = "FL-999-ZZ",
            Make = "Summit",
            Model = "Horizon Pro",
            Year = 2025,
            VehicleCategoryId = category.Id,
            DailyRate = 120.00m,
            Transmission = "Automatic",
            FuelType = "Electric",
            SeatingCapacity = "7 Passengers",
            HubLocation = "Uptown Station",
            Mileage = 500
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DuplicateException>(() => service.CreateVehicleAsync(request2));
        Assert.Contains("VIN", ex.Message);
    }

    [Fact]
    public async Task CreateVehicleAsync_DuplicateLicensePlate_ThrowsDuplicateException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var category = await SeedCategoryAsync(context, "Executive Sedan");
        var service = new VehicleService(context);

        var request1 = new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029184",
            LicensePlate = "FL-902-XP",
            Make = "Aero",
            Model = "Apex Executive Sedan",
            Year = 2025,
            VehicleCategoryId = category.Id,
            DailyRate = 85.00m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Metro Hub",
            Mileage = 1000
        };

        await service.CreateVehicleAsync(request1);

        var request2 = new CreateVehicleRequest
        {
            Vin = "5UXKR0C58K0L84912",
            LicensePlate = "fl-902-xp", // duplicate license plate (case-insensitive)
            Make = "Summit",
            Model = "Horizon Pro",
            Year = 2025,
            VehicleCategoryId = category.Id,
            DailyRate = 120.00m,
            Transmission = "Automatic",
            FuelType = "Electric",
            SeatingCapacity = "7 Passengers",
            HubLocation = "Uptown Station",
            Mileage = 500
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DuplicateException>(() => service.CreateVehicleAsync(request2));
        Assert.Contains("license plate", ex.Message);
    }

    [Fact]
    public async Task CreateVehicleAsync_NonExistentCategory_ThrowsValidationException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new VehicleService(context);

        var request = new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029184",
            LicensePlate = "FL-902-XP",
            Make = "Aero",
            Model = "Apex",
            Year = 2025,
            VehicleCategoryId = Guid.NewGuid(), // non-existent category
            DailyRate = 85.00m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Metro Hub",
            Mileage = 1000
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateVehicleAsync(request));
        Assert.Contains("does not exist", ex.Message);
    }

    [Theory]
    [InlineData("123", "VIN must be exactly 17 characters.")]
    [InlineData("123456789012345678", "VIN must be exactly 17 characters.")]
    [InlineData("", "VIN must be exactly 17 characters.")]
    public async Task CreateVehicleAsync_InvalidVinLength_ThrowsValidationException(string invalidVin, string expectedError)
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var category = await SeedCategoryAsync(context);
        var service = new VehicleService(context);

        var request = new CreateVehicleRequest
        {
            Vin = invalidVin,
            LicensePlate = "FL-902-XP",
            Make = "Aero",
            Model = "Apex",
            Year = 2025,
            VehicleCategoryId = category.Id,
            DailyRate = 85.00m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Metro Hub",
            Mileage = 1000
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateVehicleAsync(request));
        Assert.Contains(expectedError, ex.Message);
    }

    [Fact]
    public async Task CreateVehicleAsync_InvalidDailyRate_ThrowsValidationException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var category = await SeedCategoryAsync(context);
        var service = new VehicleService(context);

        var request = new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029184",
            LicensePlate = "FL-902-XP",
            Make = "Aero",
            Model = "Apex",
            Year = 2025,
            VehicleCategoryId = category.Id,
            DailyRate = -10.00m, // Invalid daily rate
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Metro Hub",
            Mileage = 1000
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateVehicleAsync(request));
        Assert.Contains("Daily rate", ex.Message);
    }

    [Fact]
    public async Task CreateVehicleAsync_InvalidMileage_ThrowsValidationException()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var category = await SeedCategoryAsync(context);
        var service = new VehicleService(context);

        var request = new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029184",
            LicensePlate = "FL-902-XP",
            Make = "Aero",
            Model = "Apex",
            Year = 2025,
            VehicleCategoryId = category.Id,
            DailyRate = 85.00m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Metro Hub",
            Mileage = -5 // Invalid negative mileage
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateVehicleAsync(request));
        Assert.Contains("Mileage", ex.Message);
    }

    [Fact]
    public async Task GetVehicleByIdAsync_ExistingId_ReturnsVehicleResponse()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var category = await SeedCategoryAsync(context, "Compact EV");
        var service = new VehicleService(context);

        var created = await service.CreateVehicleAsync(new CreateVehicleRequest
        {
            Vin = "1FTFW1E84PKD92841",
            LicensePlate = "FL-309-MN",
            Make = "Aero",
            Model = "Voltline",
            Year = 2025,
            VehicleCategoryId = category.Id,
            DailyRate = 65.00m,
            Transmission = "Direct Drive",
            FuelType = "100% Electric",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Central Depot",
            Mileage = 2000
        });

        // Act
        var result = await service.GetVehicleByIdAsync(created.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal("Voltline", result.Model);
        Assert.Equal("Compact EV", result.CategoryName);
    }

    [Fact]
    public async Task GetVehicleByIdAsync_NonExistentId_ReturnsNull()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new VehicleService(context);

        // Act
        var result = await service.GetVehicleByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetVehiclesAsync_FilteringAndSearching_ReturnsExpectedResults()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var cat1 = await SeedCategoryAsync(context, "Executive Sedan");
        var cat2 = await SeedCategoryAsync(context, "Commercial Cargo");
        var service = new VehicleService(context);

        await service.CreateVehicleAsync(new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029184",
            LicensePlate = "FL-902-XP",
            Make = "Aero",
            Model = "Apex",
            Year = 2025,
            VehicleCategoryId = cat1.Id,
            DailyRate = 85.00m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Metro Hub",
            Mileage = 1000
        });

        await service.CreateVehicleAsync(new CreateVehicleRequest
        {
            Vin = "WD3PF4CD8MP392810",
            LicensePlate = "FL-718-BK",
            Make = "Vanguard",
            Model = "TransRoute",
            Year = 2024,
            VehicleCategoryId = cat2.Id,
            DailyRate = 95.00m,
            Transmission = "Automatic",
            FuelType = "Diesel",
            SeatingCapacity = "2 Passengers",
            HubLocation = "Logistics Depot",
            Mileage = 5000
        });

        // Act - Filter by category
        var catFilterResult = await service.GetVehiclesAsync(category: "Executive Sedan");
        Assert.Single(catFilterResult);
        Assert.Equal("Aero", catFilterResult.First().Make);

        // Act - Filter by fuel
        var fuelFilterResult = await service.GetVehiclesAsync(fuel: "Diesel");
        Assert.Single(fuelFilterResult);
        Assert.Equal("Vanguard", fuelFilterResult.First().Make);

        // Act - Search by plate
        var searchResult = await service.GetVehiclesAsync(searchTerm: "718-BK");
        Assert.Single(searchResult);
        Assert.Equal("TransRoute", searchResult.First().Model);

        // Act - Pagination
        var paginatedResult = await service.GetVehiclesAsync(page: 1, pageSize: 1);
        Assert.Single(paginatedResult);
    }

    [Fact]
    public async Task GetCategoriesAsync_ReturnsAllCategoriesWithVehicleCount()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var cat1 = await SeedCategoryAsync(context, "Executive Sedan");
        var cat2 = await SeedCategoryAsync(context, "Full-Size SUV");
        var service = new VehicleService(context);

        await service.CreateVehicleAsync(new CreateVehicleRequest
        {
            Vin = "1HGCR2F83HA029184",
            LicensePlate = "FL-902-XP",
            Make = "Aero",
            Model = "Apex",
            Year = 2025,
            VehicleCategoryId = cat1.Id,
            DailyRate = 85.00m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Metro Hub",
            Mileage = 1000
        });

        // Act
        var categories = (await service.GetCategoriesAsync()).ToList();

        // Assert
        Assert.Equal(2, categories.Count);
        var execCat = categories.First(c => c.Name == "Executive Sedan");
        Assert.Equal(1, execCat.VehicleCount);
        var suvCat = categories.First(c => c.Name == "Full-Size SUV");
        Assert.Equal(0, suvCat.VehicleCount);
    }
}
