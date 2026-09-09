using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using FleetService.Api.Controllers;
using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Exceptions;
using FleetService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
using VehicleServiceImpl = FleetService.Api.Services.VehicleService;

namespace FleetFlow.VehicleService.Tests;

public class VehicleEditTests
{
    private async Task<(FleetDbContext dbContext, VehicleServiceImpl vehicleService, VehiclesController controller, VehicleCategory category, Vehicle vehicle)> CreateFixtureAsync()
    {
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new FleetDbContext(options);

        var category = new VehicleCategory
        {
            Id = Guid.NewGuid(),
            Name = "Executive Sedan",
            Description = "Luxury sedans"
        };
        dbContext.VehicleCategories.Add(category);

        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            Vin = "1HGCR2F83HA000001",
            LicensePlate = "WP-CAB-1001",
            Make = "Honda",
            Model = "Accord",
            Year = 2024,
            VehicleCategoryId = category.Id,
            Category = category,
            DailyRate = 18500m,
            Transmission = "Automatic",
            FuelType = "Gasoline",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Colombo Fort Hub",
            Mileage = 12500,
            Status = VehicleStatus.Available,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = null
        };
        dbContext.Vehicles.Add(vehicle);
        await dbContext.SaveChangesAsync();

        var vehicleService = new VehicleServiceImpl(dbContext);
        var controller = new VehiclesController(vehicleService);

        return (dbContext, vehicleService, controller, category, vehicle);
    }

    private static void SetUserContext(ControllerBase controller, string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, "testuser"),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    // ==========================================
    // 1. AUTHORIZED UPDATES (200 OK)
    // ==========================================

    [Fact]
    public async Task UpdateVehicle_AsFleetManager_Returns200AndUpdatesFields()
    {
        var (dbContext, _, controller, category, vehicle) = await CreateFixtureAsync();
        SetUserContext(controller, "FLEET_MANAGER");

        var originalCreatedAt = vehicle.CreatedAt;
        var originalId = vehicle.Id;
        var originalStatus = vehicle.Status;

        var request = new UpdateVehicleRequest
        {
            Vin = vehicle.Vin,
            LicensePlate = "WP-CAB-9999",
            Make = "Honda",
            Model = "Accord Hybrid",
            Year = 2025,
            VehicleCategoryId = category.Id,
            DailyRate = 22000m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Kandy Central Station",
            Mileage = 15000
        };

        var result = await controller.UpdateVehicle(vehicle.Id, request);

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = okResult.Value as VehicleResponse;
        response.Should().NotBeNull();
        response!.LicensePlate.Should().Be("WP-CAB-9999");
        response.Model.Should().Be("Accord Hybrid");
        response.Year.Should().Be(2025);
        response.DailyRate.Should().Be(22000m);
        response.HubLocation.Should().Be("Kandy Central Station");
        response.UpdatedAt.Should().NotBeNull();

        // Verify persistence in DB
        var updatedInDb = await dbContext.Vehicles.FindAsync(vehicle.Id);
        updatedInDb.Should().NotBeNull();
        updatedInDb!.LicensePlate.Should().Be("WP-CAB-9999");
        updatedInDb.Model.Should().Be("Accord Hybrid");
        updatedInDb.DailyRate.Should().Be(22000m);
        updatedInDb.UpdatedAt.Should().NotBeNull();

        // Verify immutability of Id, CreatedAt, Status
        updatedInDb.Id.Should().Be(originalId);
        updatedInDb.CreatedAt.Should().Be(originalCreatedAt);
        updatedInDb.Status.Should().Be(originalStatus);
    }

    [Fact]
    public async Task UpdateVehicle_AsAdmin_Returns200Ok()
    {
        var (dbContext, _, controller, category, vehicle) = await CreateFixtureAsync();
        SetUserContext(controller, "ADMIN");

        var request = new UpdateVehicleRequest
        {
            Vin = vehicle.Vin,
            LicensePlate = vehicle.LicensePlate,
            Make = "Honda",
            Model = "Accord Touring",
            Year = 2024,
            VehicleCategoryId = category.Id,
            DailyRate = 25000m,
            Transmission = "Automatic",
            FuelType = "Gasoline",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Colombo Fort Hub",
            Mileage = 13000
        };

        var result = await controller.UpdateVehicle(vehicle.Id, request);

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = okResult.Value as VehicleResponse;
        response!.Model.Should().Be("Accord Touring");
        response.DailyRate.Should().Be(25000m);
    }

    // ==========================================
    // 2. ROLE AUTHORIZATION DECORATION (401 / 403)
    // ==========================================

    [Fact]
    public void UpdateVehicleEndpoint_HasAuthorizeAttributeWithFleetManagerAndAdmin()
    {
        var method = typeof(VehiclesController).GetMethod(nameof(VehiclesController.UpdateVehicle));
        method.Should().NotBeNull();

        var authorizeAttr = method!.GetCustomAttribute<AuthorizeAttribute>();
        authorizeAttr.Should().NotBeNull();
        authorizeAttr!.Roles.Should().Be("FLEET_MANAGER,ADMIN");
    }

    // ==========================================
    // 3. NOT FOUND & VALIDATION ERRORS (404 / 400)
    // ==========================================

    [Fact]
    public async Task UpdateVehicle_NonExistentVehicle_Returns404NotFound()
    {
        var (_, _, controller, category, _) = await CreateFixtureAsync();
        SetUserContext(controller, "FLEET_MANAGER");

        var missingId = Guid.NewGuid();
        var request = new UpdateVehicleRequest
        {
            Vin = "1HGCR2F83HA000001",
            LicensePlate = "WP-CAB-1001",
            Make = "Honda",
            Model = "Accord",
            Year = 2024,
            VehicleCategoryId = category.Id,
            DailyRate = 18500m,
            Transmission = "Automatic",
            FuelType = "Gasoline",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Colombo Fort Hub",
            Mileage = 12500
        };

        var result = await controller.UpdateVehicle(missingId, request);

        var notFoundResult = result.Result as NotFoundObjectResult;
        notFoundResult.Should().NotBeNull();
        notFoundResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UpdateVehicle_InvalidDailyRate_Returns400BadRequest()
    {
        var (_, _, controller, category, vehicle) = await CreateFixtureAsync();
        SetUserContext(controller, "FLEET_MANAGER");

        var request = new UpdateVehicleRequest
        {
            Vin = vehicle.Vin,
            LicensePlate = vehicle.LicensePlate,
            Make = "Honda",
            Model = "Accord",
            Year = 2024,
            VehicleCategoryId = category.Id,
            DailyRate = -100m, // Invalid rate <= 0
            Transmission = "Automatic",
            FuelType = "Gasoline",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Colombo Fort Hub",
            Mileage = 12500
        };

        var result = await controller.UpdateVehicle(vehicle.Id, request);

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task UpdateVehicle_InvalidCategory_Returns400BadRequest()
    {
        var (_, _, controller, _, vehicle) = await CreateFixtureAsync();
        SetUserContext(controller, "FLEET_MANAGER");

        var request = new UpdateVehicleRequest
        {
            Vin = vehicle.Vin,
            LicensePlate = vehicle.LicensePlate,
            Make = "Honda",
            Model = "Accord",
            Year = 2024,
            VehicleCategoryId = Guid.NewGuid(), // Non-existent category
            DailyRate = 18500m,
            Transmission = "Automatic",
            FuelType = "Gasoline",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Colombo Fort Hub",
            Mileage = 12500
        };

        var result = await controller.UpdateVehicle(vehicle.Id, request);

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    // ==========================================
    // 4. DUPLICATE VIN / PLATE CONFLICTS (409)
    // ==========================================

    [Fact]
    public async Task UpdateVehicle_DuplicatePlateOnAnotherVehicle_Returns409Conflict()
    {
        var (dbContext, _, controller, category, vehicle) = await CreateFixtureAsync();
        SetUserContext(controller, "FLEET_MANAGER");

        // Add a second vehicle with a different plate
        var secondVehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            Vin = "2HGCR2F83HA000002",
            LicensePlate = "WP-XYZ-8888",
            Make = "Toyota",
            Model = "Camry",
            Year = 2024,
            VehicleCategoryId = category.Id,
            DailyRate = 19000m,
            Transmission = "Automatic",
            FuelType = "Gasoline",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Colombo Fort Hub",
            Mileage = 5000,
            Status = VehicleStatus.Available,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Vehicles.Add(secondVehicle);
        await dbContext.SaveChangesAsync();

        // Attempt to update vehicle 1 with vehicle 2's plate
        var request = new UpdateVehicleRequest
        {
            Vin = vehicle.Vin,
            LicensePlate = "wp-xyz-8888", // duplicate case-insensitive
            Make = vehicle.Make,
            Model = vehicle.Model,
            Year = vehicle.Year,
            VehicleCategoryId = category.Id,
            DailyRate = vehicle.DailyRate,
            Transmission = vehicle.Transmission,
            FuelType = vehicle.FuelType,
            SeatingCapacity = vehicle.SeatingCapacity,
            HubLocation = vehicle.HubLocation,
            Mileage = vehicle.Mileage
        };

        var result = await controller.UpdateVehicle(vehicle.Id, request);

        var conflictResult = result.Result as ConflictObjectResult;
        conflictResult.Should().NotBeNull();
        conflictResult!.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task UpdateVehicle_DuplicateVinOnAnotherVehicle_Returns409Conflict()
    {
        var (dbContext, _, controller, category, vehicle) = await CreateFixtureAsync();
        SetUserContext(controller, "FLEET_MANAGER");

        var secondVehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            Vin = "2HGCR2F83HA000002",
            LicensePlate = "WP-XYZ-8888",
            Make = "Toyota",
            Model = "Camry",
            Year = 2024,
            VehicleCategoryId = category.Id,
            DailyRate = 19000m,
            Transmission = "Automatic",
            FuelType = "Gasoline",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Colombo Fort Hub",
            Mileage = 5000,
            Status = VehicleStatus.Available,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Vehicles.Add(secondVehicle);
        await dbContext.SaveChangesAsync();

        // Attempt to update vehicle 1 with vehicle 2's VIN
        var request = new UpdateVehicleRequest
        {
            Vin = "2hgcr2f83ha000002", // duplicate case-insensitive
            LicensePlate = vehicle.LicensePlate,
            Make = vehicle.Make,
            Model = vehicle.Model,
            Year = vehicle.Year,
            VehicleCategoryId = category.Id,
            DailyRate = vehicle.DailyRate,
            Transmission = vehicle.Transmission,
            FuelType = vehicle.FuelType,
            SeatingCapacity = vehicle.SeatingCapacity,
            HubLocation = vehicle.HubLocation,
            Mileage = vehicle.Mileage
        };

        var result = await controller.UpdateVehicle(vehicle.Id, request);

        var conflictResult = result.Result as ConflictObjectResult;
        conflictResult.Should().NotBeNull();
        conflictResult!.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task UpdateVehicle_RetainingOwnPlateAndVin_Returns200Ok()
    {
        var (_, _, controller, category, vehicle) = await CreateFixtureAsync();
        SetUserContext(controller, "FLEET_MANAGER");

        var request = new UpdateVehicleRequest
        {
            Vin = vehicle.Vin,
            LicensePlate = vehicle.LicensePlate,
            Make = "Honda",
            Model = "Accord Refined",
            Year = vehicle.Year,
            VehicleCategoryId = category.Id,
            DailyRate = vehicle.DailyRate,
            Transmission = vehicle.Transmission,
            FuelType = vehicle.FuelType,
            SeatingCapacity = vehicle.SeatingCapacity,
            HubLocation = vehicle.HubLocation,
            Mileage = vehicle.Mileage
        };

        var result = await controller.UpdateVehicle(vehicle.Id, request);

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);
    }
}
