using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Services;
using Xunit;

namespace FleetService.Tests;

public class VehicleSearchFilterPaginationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public VehicleSearchFilterPaginationTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private static FleetDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new FleetDbContext(options);
    }

    private static async Task<VehicleCategory> SeedCategoryAsync(FleetDbContext context, string name)
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

    private static async Task SeedSampleFleetAsync(FleetDbContext context, VehicleCategory sedanCat, VehicleCategory suvCat, VehicleCategory cargoCat)
    {
        var vehicles = new List<Vehicle>
        {
            new Vehicle
            {
                Id = Guid.NewGuid(),
                Vin = "1HGCR2F83HA000001",
                LicensePlate = "FL-001-AA",
                Make = "Toyota",
                Model = "Camry",
                Year = 2024,
                VehicleCategoryId = sedanCat.Id,
                DailyRate = 65.00m,
                Transmission = "Automatic",
                FuelType = "Hybrid",
                SeatingCapacity = "5 Passengers",
                HubLocation = "Metro Hub",
                Mileage = 12000,
                Status = VehicleStatus.Available,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            },
            new Vehicle
            {
                Id = Guid.NewGuid(),
                Vin = "2T3P1RFV5PW000002",
                LicensePlate = "FL-002-BB",
                Make = "Toyota",
                Model = "RAV4",
                Year = 2023,
                VehicleCategoryId = suvCat.Id,
                DailyRate = 85.00m,
                Transmission = "Automatic",
                FuelType = "Gasoline",
                SeatingCapacity = "5 Passengers",
                HubLocation = "Airport Terminal 2",
                Mileage = 25000,
                Status = VehicleStatus.InUse,
                CreatedAt = DateTime.UtcNow.AddDays(-8)
            },
            new Vehicle
            {
                Id = Guid.NewGuid(),
                Vin = "3VW2K7AJ8PM000003",
                LicensePlate = "FL-003-CC",
                Make = "Volkswagen",
                Model = "Golf",
                Year = 2022,
                VehicleCategoryId = sedanCat.Id,
                DailyRate = 50.00m,
                Transmission = "Manual",
                FuelType = "Diesel",
                SeatingCapacity = "5 Passengers",
                HubLocation = "Metro Hub",
                Mileage = 40000,
                Status = VehicleStatus.Available,
                CreatedAt = DateTime.UtcNow.AddDays(-6)
            },
            new Vehicle
            {
                Id = Guid.NewGuid(),
                Vin = "4S3BTANC4R3000004",
                LicensePlate = "FL-004-DD",
                Make = "Ford",
                Model = "Transit",
                Year = 2025,
                VehicleCategoryId = cargoCat.Id,
                DailyRate = 110.00m,
                Transmission = "Automatic",
                FuelType = "Electric",
                SeatingCapacity = "2 Passengers",
                HubLocation = "Logistics Depot",
                Mileage = 5000,
                Status = VehicleStatus.Maintenance,
                CreatedAt = DateTime.UtcNow.AddDays(-4)
            },
            new Vehicle
            {
                Id = Guid.NewGuid(),
                Vin = "5YJSA1E28MF000005",
                LicensePlate = "FL-005-EE",
                Make = "Tesla",
                Model = "Model 3",
                Year = 2024,
                VehicleCategoryId = sedanCat.Id,
                DailyRate = 95.00m,
                Transmission = "Automatic",
                FuelType = "Electric",
                SeatingCapacity = "5 Passengers",
                HubLocation = "Metro Hub",
                Mileage = 8000,
                Status = VehicleStatus.Available,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            }
        };

        context.Vehicles.AddRange(vehicles);
        await context.SaveChangesAsync();
    }

    // =========================================================================
    // SEARCH TESTS
    // =========================================================================

    [Fact]
    public async Task Search_ByVin_ReturnsMatchingVehicle()
    {
        using var context = CreateInMemoryDbContext();
        var sedan = await SeedCategoryAsync(context, "Sedan");
        var suv = await SeedCategoryAsync(context, "SUV");
        var cargo = await SeedCategoryAsync(context, "Cargo");
        await SeedSampleFleetAsync(context, sedan, suv, cargo);

        var service = new VehicleService(context);

        var result = await service.GetPagedVehiclesAsync(new VehicleQueryParameters
        {
            SearchTerm = "000003"
        });

        Assert.Single(result.Items);
        Assert.Equal("3VW2K7AJ8PM000003", result.Items.First().Vin);
    }

    [Fact]
    public async Task Search_ByLicensePlate_ReturnsMatchingVehicle()
    {
        using var context = CreateInMemoryDbContext();
        var sedan = await SeedCategoryAsync(context, "Sedan");
        var suv = await SeedCategoryAsync(context, "SUV");
        var cargo = await SeedCategoryAsync(context, "Cargo");
        await SeedSampleFleetAsync(context, sedan, suv, cargo);

        var service = new VehicleService(context);

        var result = await service.GetPagedVehiclesAsync(new VehicleQueryParameters
        {
            SearchTerm = "FL-004-DD"
        });

        Assert.Single(result.Items);
        Assert.Equal("Ford", result.Items.First().Make);
    }

    [Fact]
    public async Task Search_ByMake_ReturnsMatchingVehicles()
    {
        using var context = CreateInMemoryDbContext();
        var sedan = await SeedCategoryAsync(context, "Sedan");
        var suv = await SeedCategoryAsync(context, "SUV");
        var cargo = await SeedCategoryAsync(context, "Cargo");
        await SeedSampleFleetAsync(context, sedan, suv, cargo);

        var service = new VehicleService(context);

        var result = await service.GetPagedVehiclesAsync(new VehicleQueryParameters
        {
            SearchTerm = "toyota"
        });

        Assert.Equal(2, result.Items.Count());
        Assert.All(result.Items, v => Assert.Equal("Toyota", v.Make));
    }

    [Fact]
    public async Task Search_ByModel_ReturnsMatchingVehicle()
    {
        using var context = CreateInMemoryDbContext();
        var sedan = await SeedCategoryAsync(context, "Sedan");
        var suv = await SeedCategoryAsync(context, "SUV");
        var cargo = await SeedCategoryAsync(context, "Cargo");
        await SeedSampleFleetAsync(context, sedan, suv, cargo);

        var service = new VehicleService(context);

        var result = await service.GetPagedVehiclesAsync(new VehicleQueryParameters
        {
            SearchTerm = "Transit"
        });

        Assert.Single(result.Items);
        Assert.Equal("Transit", result.Items.First().Model);
    }

    [Fact]
    public async Task Search_ByCombinedMakeAndModel_ReturnsMatchingVehicle()
    {
        using var context = CreateInMemoryDbContext();
        var sedan = await SeedCategoryAsync(context, "Sedan");
        var suv = await SeedCategoryAsync(context, "SUV");
        var cargo = await SeedCategoryAsync(context, "Cargo");
        await SeedSampleFleetAsync(context, sedan, suv, cargo);

        var service = new VehicleService(context);

        var result = await service.GetPagedVehiclesAsync(new VehicleQueryParameters
        {
            SearchTerm = "Toyota Camry"
        });

        Assert.Single(result.Items);
        Assert.Equal("Camry", result.Items.First().Model);
    }

    // =========================================================================
    // FILTER TESTS
    // =========================================================================

    [Fact]
    public async Task Filter_ByCategory_ReturnsOnlyMatchingVehicles()
    {
        using var context = CreateInMemoryDbContext();
        var sedan = await SeedCategoryAsync(context, "Executive Sedan");
        var suv = await SeedCategoryAsync(context, "Full-Size SUV");
        var cargo = await SeedCategoryAsync(context, "Commercial Cargo");
        await SeedSampleFleetAsync(context, sedan, suv, cargo);

        var service = new VehicleService(context);

        var result = await service.GetPagedVehiclesAsync(new VehicleQueryParameters
        {
            Category = "Full-Size SUV"
        });

        Assert.Single(result.Items);
        Assert.Equal("RAV4", result.Items.First().Model);
    }

    [Fact]
    public async Task Filter_ByStatus_ReturnsOnlyMatchingVehicles()
    {
        using var context = CreateInMemoryDbContext();
        var sedan = await SeedCategoryAsync(context, "Sedan");
        var suv = await SeedCategoryAsync(context, "SUV");
        var cargo = await SeedCategoryAsync(context, "Cargo");
        await SeedSampleFleetAsync(context, sedan, suv, cargo);

        var service = new VehicleService(context);

        var result = await service.GetPagedVehiclesAsync(new VehicleQueryParameters
        {
            Status = VehicleStatus.Maintenance
        });

        Assert.Single(result.Items);
        Assert.Equal("Ford", result.Items.First().Make);
    }

    [Fact]
    public async Task Filter_ByFuelType_ReturnsOnlyMatchingVehicles()
    {
        using var context = CreateInMemoryDbContext();
        var sedan = await SeedCategoryAsync(context, "Sedan");
        var suv = await SeedCategoryAsync(context, "SUV");
        var cargo = await SeedCategoryAsync(context, "Cargo");
        await SeedSampleFleetAsync(context, sedan, suv, cargo);

        var service = new VehicleService(context);

        var result = await service.GetPagedVehiclesAsync(new VehicleQueryParameters
        {
            Fuel = "Electric"
        });

        Assert.Equal(2, result.Items.Count());
        Assert.All(result.Items, v => Assert.Equal("Electric", v.FuelType));
    }

    [Fact]
    public async Task Filter_ByTransmission_ReturnsOnlyMatchingVehicles()
    {
        using var context = CreateInMemoryDbContext();
        var sedan = await SeedCategoryAsync(context, "Sedan");
        var suv = await SeedCategoryAsync(context, "SUV");
        var cargo = await SeedCategoryAsync(context, "Cargo");
        await SeedSampleFleetAsync(context, sedan, suv, cargo);

        var service = new VehicleService(context);

        var result = await service.GetPagedVehiclesAsync(new VehicleQueryParameters
        {
            Transmission = "Manual"
        });

        Assert.Single(result.Items);
        Assert.Equal("Golf", result.Items.First().Model);
    }

    [Fact]
    public async Task Filter_ByHubLocation_ReturnsOnlyMatchingVehicles()
    {
        using var context = CreateInMemoryDbContext();
        var sedan = await SeedCategoryAsync(context, "Sedan");
        var suv = await SeedCategoryAsync(context, "SUV");
        var cargo = await SeedCategoryAsync(context, "Cargo");
        await SeedSampleFleetAsync(context, sedan, suv, cargo);

        var service = new VehicleService(context);

        var result = await service.GetPagedVehiclesAsync(new VehicleQueryParameters
        {
            Hub = "Logistics Depot"
        });

        Assert.Single(result.Items);
        Assert.Equal("Transit", result.Items.First().Model);
    }

    [Fact]
    public async Task Filter_CombinedSearchAndMultiFilters_ReturnsCorrectIntersection()
    {
        using var context = CreateInMemoryDbContext();
        var sedan = await SeedCategoryAsync(context, "Executive Sedan");
        var suv = await SeedCategoryAsync(context, "Full-Size SUV");
        var cargo = await SeedCategoryAsync(context, "Commercial Cargo");
        await SeedSampleFleetAsync(context, sedan, suv, cargo);

        var service = new VehicleService(context);

        // Filter: Category = Executive Sedan, Status = Available, Fuel = Electric, Transmission = Automatic, Hub = Metro Hub
        var result = await service.GetPagedVehiclesAsync(new VehicleQueryParameters
        {
            Category = "Executive Sedan",
            Status = VehicleStatus.Available,
            Fuel = "Electric",
            Transmission = "Automatic",
            Hub = "Metro Hub",
            SearchTerm = "Tesla"
        });

        Assert.Single(result.Items);
        var vehicle = result.Items.First();
        Assert.Equal("Tesla", vehicle.Make);
        Assert.Equal("Model 3", vehicle.Model);
        Assert.Equal(VehicleStatus.Available, vehicle.Status);
    }

    // =========================================================================
    // SORTING TESTS
    // =========================================================================

    [Fact]
    public async Task Sorting_ByDailyRate_AscendingAndDescending()
    {
        using var context = CreateInMemoryDbContext();
        var sedan = await SeedCategoryAsync(context, "Sedan");
        var suv = await SeedCategoryAsync(context, "SUV");
        var cargo = await SeedCategoryAsync(context, "Cargo");
        await SeedSampleFleetAsync(context, sedan, suv, cargo);

        var service = new VehicleService(context);

        // Ascending
        var ascResult = await service.GetPagedVehiclesAsync(new VehicleQueryParameters
        {
            SortBy = "dailyRate",
            SortOrder = "asc"
        });
        var ratesAsc = ascResult.Items.Select(v => v.DailyRate).ToList();
        Assert.Equal(50.00m, ratesAsc.First());
        Assert.Equal(110.00m, ratesAsc.Last());

        // Descending
        var descResult = await service.GetPagedVehiclesAsync(new VehicleQueryParameters
        {
            SortBy = "dailyRate",
            SortOrder = "desc"
        });
        var ratesDesc = descResult.Items.Select(v => v.DailyRate).ToList();
        Assert.Equal(110.00m, ratesDesc.First());
        Assert.Equal(50.00m, ratesDesc.Last());
    }

    [Fact]
    public async Task Sorting_ByYear_Descending()
    {
        using var context = CreateInMemoryDbContext();
        var sedan = await SeedCategoryAsync(context, "Sedan");
        var suv = await SeedCategoryAsync(context, "SUV");
        var cargo = await SeedCategoryAsync(context, "Cargo");
        await SeedSampleFleetAsync(context, sedan, suv, cargo);

        var service = new VehicleService(context);

        var result = await service.GetPagedVehiclesAsync(new VehicleQueryParameters
        {
            SortBy = "year",
            SortOrder = "desc"
        });

        var years = result.Items.Select(v => v.Year).ToList();
        Assert.Equal(2025, years.First());
        Assert.Equal(2022, years.Last());
    }

    [Fact]
    public async Task Sorting_ByMileage_Ascending()
    {
        using var context = CreateInMemoryDbContext();
        var sedan = await SeedCategoryAsync(context, "Sedan");
        var suv = await SeedCategoryAsync(context, "SUV");
        var cargo = await SeedCategoryAsync(context, "Cargo");
        await SeedSampleFleetAsync(context, sedan, suv, cargo);

        var service = new VehicleService(context);

        var result = await service.GetPagedVehiclesAsync(new VehicleQueryParameters
        {
            SortBy = "mileage",
            SortOrder = "asc"
        });

        var mileages = result.Items.Select(v => v.Mileage).ToList();
        Assert.Equal(5000, mileages.First());
        Assert.Equal(40000, mileages.Last());
    }

    // =========================================================================
    // PAGINATION TESTS
    // =========================================================================

    [Fact]
    public async Task Pagination_ReturnsCorrectPageSliceAndMetadata()
    {
        using var context = CreateInMemoryDbContext();
        var sedan = await SeedCategoryAsync(context, "Sedan");
        var suv = await SeedCategoryAsync(context, "SUV");
        var cargo = await SeedCategoryAsync(context, "Cargo");
        await SeedSampleFleetAsync(context, sedan, suv, cargo);

        var service = new VehicleService(context);

        // Page 1 with pageSize 2
        var page1 = await service.GetPagedVehiclesAsync(new VehicleQueryParameters
        {
            Page = 1,
            PageSize = 2
        });

        Assert.Equal(2, page1.Items.Count());
        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.TotalPages);
        Assert.Equal(1, page1.Page);
        Assert.True(page1.HasNextPage);
        Assert.False(page1.HasPreviousPage);

        // Page 2 with pageSize 2
        var page2 = await service.GetPagedVehiclesAsync(new VehicleQueryParameters
        {
            Page = 2,
            PageSize = 2
        });

        Assert.Equal(2, page2.Items.Count());
        Assert.True(page2.HasNextPage);
        Assert.True(page2.HasPreviousPage);

        // Items on page 1 and page 2 should be disjoint
        var page1Ids = page1.Items.Select(v => v.Id).ToHashSet();
        var page2Ids = page2.Items.Select(v => v.Id).ToHashSet();
        Assert.Empty(page1Ids.Intersect(page2Ids));

        // Page 3 with pageSize 2 (only 1 item remaining)
        var page3 = await service.GetPagedVehiclesAsync(new VehicleQueryParameters
        {
            Page = 3,
            PageSize = 2
        });

        Assert.Single(page3.Items);
        Assert.False(page3.HasNextPage);
        Assert.True(page3.HasPreviousPage);
    }

    [Fact]
    public async Task Pagination_InvalidPageAndPageSize_HandledGracefully()
    {
        using var context = CreateInMemoryDbContext();
        var sedan = await SeedCategoryAsync(context, "Sedan");
        var suv = await SeedCategoryAsync(context, "SUV");
        var cargo = await SeedCategoryAsync(context, "Cargo");
        await SeedSampleFleetAsync(context, sedan, suv, cargo);

        var service = new VehicleService(context);

        // Negative page and pageSize 0 should default safely
        var result = await service.GetPagedVehiclesAsync(new VehicleQueryParameters
        {
            Page = -5,
            PageSize = 0
        });

        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(5, result.TotalCount);

        // Oversized pageSize should be clamped to 100
        var clampedResult = await service.GetPagedVehiclesAsync(new VehicleQueryParameters
        {
            Page = 1,
            PageSize = 500
        });

        Assert.Equal(100, clampedResult.PageSize);
    }

    [Fact]
    public async Task Search_NoMatchingResults_ReturnsEmptyItemsAndZeroCount()
    {
        using var context = CreateInMemoryDbContext();
        var sedan = await SeedCategoryAsync(context, "Sedan");
        var suv = await SeedCategoryAsync(context, "SUV");
        var cargo = await SeedCategoryAsync(context, "Cargo");
        await SeedSampleFleetAsync(context, sedan, suv, cargo);

        var service = new VehicleService(context);

        var result = await service.GetPagedVehiclesAsync(new VehicleQueryParameters
        {
            SearchTerm = "NON_EXISTENT_VEHICLE_VIN_123456789"
        });

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    // =========================================================================
    // CONTROLLER HTTP ENDPOINT INTEGRATION TESTS
    // =========================================================================

    [Fact]
    public async Task Controller_GetVehicles_SetsPaginationHeaders()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/vehicles?page=1&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Total-Count"));
        Assert.True(response.Headers.Contains("X-Page"));
        Assert.True(response.Headers.Contains("X-Page-Size"));
        Assert.True(response.Headers.Contains("X-Total-Pages"));

        // Default endpoint returns array for backward compatibility
        var list = await response.Content.ReadFromJsonAsync<List<VehicleResponse>>();
        Assert.NotNull(list);
    }

    [Fact]
    public async Task Controller_GetVehicles_PagedTrue_ReturnsPagedVehicleResult()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/vehicles?paged=true&page=1&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pagedResult = await response.Content.ReadFromJsonAsync<PagedVehicleResult>();

        Assert.NotNull(pagedResult);
        Assert.NotNull(pagedResult.Items);
        Assert.Equal(1, pagedResult.Page);
        Assert.Equal(5, pagedResult.PageSize);
    }

    [Fact]
    public async Task Controller_GetPagedVehicles_Endpoint_Returns200OkWithPagedResult()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/vehicles/paged?transmission=Automatic&page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedVehicleResult>();

        Assert.NotNull(result);
        Assert.NotNull(result.Items);
        Assert.Equal(1, result.Page);
    }
}
