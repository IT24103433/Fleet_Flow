using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using FleetService.Api.Controllers;
using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Messaging;
using FleetService.Api.Messaging.Events;
using FleetService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using VehicleServiceImpl = FleetService.Api.Services.VehicleService;

namespace FleetFlow.VehicleService.Tests;

public class VehicleStatusTests
{
    private async Task<(FleetDbContext dbContext, Mock<IKafkaProducerService> kafkaMock, VehicleServiceImpl vehicleService, VehiclesController controller, VehicleCategory category, Vehicle vehicle)> CreateFixtureAsync()
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

        var kafkaMock = new Mock<IKafkaProducerService>();
        var kafkaSettings = Options.Create(new KafkaSettings { VehicleEventsTopic = "vehicle-events" });

        var vehicleService = new VehicleServiceImpl(dbContext, kafkaMock.Object, kafkaSettings);
        var controller = new VehiclesController(vehicleService);

        return (dbContext, kafkaMock, vehicleService, controller, category, vehicle);
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

    // =========================================================================
    // 1. AUTHORIZED STATUS CHANGES (FLEET_MANAGER, ADMIN, MAINTENANCE_STAFF)
    // =========================================================================

    [Theory]
    [InlineData(VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.InUse)]
    [InlineData(VehicleStatus.Retired)]
    [InlineData(VehicleStatus.Available)]
    public async Task UpdateVehicleStatus_AsFleetManager_Returns200AndPersistsStatus(VehicleStatus targetStatus)
    {
        var (dbContext, kafkaMock, _, controller, _, vehicle) = await CreateFixtureAsync();
        SetUserContext(controller, "FLEET_MANAGER");

        var request = new UpdateVehicleStatusRequest { Status = targetStatus };

        var result = await controller.UpdateVehicleStatus(vehicle.Id, request);

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = okResult.Value as VehicleResponse;
        response.Should().NotBeNull();
        response!.Status.Should().Be(targetStatus);
        response.UpdatedAt.Should().NotBeNull();

        // Verify persistence in database
        var inDb = await dbContext.Vehicles.FindAsync(vehicle.Id);
        inDb.Should().NotBeNull();
        inDb!.Status.Should().Be(targetStatus);
        inDb.UpdatedAt.Should().NotBeNull();
        inDb.Vin.Should().Be(vehicle.Vin);
        inDb.LicensePlate.Should().Be(vehicle.LicensePlate);

        // Verify Kafka event was dispatched
        kafkaMock.Verify(k => k.PublishAsync(
            "vehicle-events",
            vehicle.Id.ToString(),
            It.Is<VehicleUpdatedEvent>(e => e.VehicleId == vehicle.Id && e.Status == targetStatus)),
            Times.Once);
    }

    [Theory]
    [InlineData(VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.InUse)]
    [InlineData(VehicleStatus.Retired)]
    [InlineData(VehicleStatus.Available)]
    public async Task UpdateVehicleStatus_AsAdmin_Returns200AndPersistsStatus(VehicleStatus targetStatus)
    {
        var (dbContext, _, _, controller, _, vehicle) = await CreateFixtureAsync();
        SetUserContext(controller, "ADMIN");

        var request = new UpdateVehicleStatusRequest { Status = targetStatus };

        var result = await controller.UpdateVehicleStatus(vehicle.Id, request);

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = okResult.Value as VehicleResponse;
        response.Should().NotBeNull();
        response!.Status.Should().Be(targetStatus);

        var inDb = await dbContext.Vehicles.FindAsync(vehicle.Id);
        inDb!.Status.Should().Be(targetStatus);
    }

    [Theory]
    [InlineData(VehicleStatus.Maintenance)]
    [InlineData(VehicleStatus.Available)]
    public async Task UpdateVehicleStatus_AsMaintenanceStaff_OperationalHealthStatuses_Returns200Ok(VehicleStatus healthStatus)
    {
        var (dbContext, _, _, controller, _, vehicle) = await CreateFixtureAsync();
        SetUserContext(controller, "MAINTENANCE_STAFF");

        var request = new UpdateVehicleStatusRequest { Status = healthStatus };

        var result = await controller.UpdateVehicleStatus(vehicle.Id, request);

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = okResult.Value as VehicleResponse;
        response!.Status.Should().Be(healthStatus);

        var inDb = await dbContext.Vehicles.FindAsync(vehicle.Id);
        inDb!.Status.Should().Be(healthStatus);
    }

    // =========================================================================
    // 2. ROLE AUTHORIZATION & PERMISSION RULES (403 FORBIDDEN / 401 UNAUTHORIZED)
    // =========================================================================

    [Fact]
    public void UpdateVehicleStatusEndpoint_HasAuthorizeAttributeWithAuthorizedStaffRoles()
    {
        var method = typeof(VehiclesController).GetMethod(nameof(VehiclesController.UpdateVehicleStatus));
        method.Should().NotBeNull();

        var authorizeAttr = method!.GetCustomAttribute<AuthorizeAttribute>();
        authorizeAttr.Should().NotBeNull();
        authorizeAttr!.Roles.Should().Be("FLEET_MANAGER,ADMIN,MAINTENANCE_STAFF");
    }

    [Fact]
    public void UpdateVehicleStatusEndpoint_AcceptsBothHttpPatchAndHttpPut()
    {
        var method = typeof(VehiclesController).GetMethod(nameof(VehiclesController.UpdateVehicleStatus));
        method.Should().NotBeNull();

        var patchAttr = method!.GetCustomAttribute<HttpPatchAttribute>();
        var putAttr = method.GetCustomAttribute<HttpPutAttribute>();

        patchAttr.Should().NotBeNull();
        patchAttr!.Template.Should().Be("{id:guid}/status");

        putAttr.Should().NotBeNull();
        putAttr!.Template.Should().Be("{id:guid}/status");
    }

    [Theory]
    [InlineData(VehicleStatus.Retired)]
    [InlineData(VehicleStatus.InUse)]
    public async Task UpdateVehicleStatus_AsMaintenanceStaff_DisallowedStatuses_Returns403Forbidden(VehicleStatus disallowedStatus)
    {
        var (dbContext, _, _, controller, _, vehicle) = await CreateFixtureAsync();
        SetUserContext(controller, "MAINTENANCE_STAFF");

        var originalStatus = vehicle.Status;
        var request = new UpdateVehicleStatusRequest { Status = disallowedStatus };

        var result = await controller.UpdateVehicleStatus(vehicle.Id, request);

        var statusResult = result.Result as ObjectResult;
        statusResult.Should().NotBeNull();
        statusResult!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        // Verify status was NOT modified in DB
        var inDb = await dbContext.Vehicles.FindAsync(vehicle.Id);
        inDb!.Status.Should().Be(originalStatus);
    }

    // =========================================================================
    // 3. VALIDATION & NOT FOUND ERRORS (400 BAD REQUEST / 404 NOT FOUND)
    // =========================================================================

    [Fact]
    public async Task UpdateVehicleStatus_NonExistentVehicle_Returns404NotFound()
    {
        var (_, _, _, controller, _, _) = await CreateFixtureAsync();
        SetUserContext(controller, "FLEET_MANAGER");

        var missingId = Guid.NewGuid();
        var request = new UpdateVehicleStatusRequest { Status = VehicleStatus.Maintenance };

        var result = await controller.UpdateVehicleStatus(missingId, request);

        var notFoundResult = result.Result as NotFoundObjectResult;
        notFoundResult.Should().NotBeNull();
        notFoundResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UpdateVehicleStatus_InvalidEnumStatus_Returns400BadRequest()
    {
        var (_, _, _, controller, _, vehicle) = await CreateFixtureAsync();
        SetUserContext(controller, "FLEET_MANAGER");

        var request = new UpdateVehicleStatusRequest { Status = (VehicleStatus)999 };

        var result = await controller.UpdateVehicleStatus(vehicle.Id, request);

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task UpdateVehicleStatus_NullPayload_Returns400BadRequest()
    {
        var (_, _, _, controller, _, vehicle) = await CreateFixtureAsync();
        SetUserContext(controller, "FLEET_MANAGER");

        var result = await controller.UpdateVehicleStatus(vehicle.Id, null!);

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task UpdateVehicleStatus_ModelErrors_Returns400BadRequest()
    {
        var (_, _, _, controller, _, vehicle) = await CreateFixtureAsync();
        SetUserContext(controller, "FLEET_MANAGER");

        controller.ModelState.AddModelError("Status", "Status is required.");
        var request = new UpdateVehicleStatusRequest { Status = VehicleStatus.Available };

        var result = await controller.UpdateVehicleStatus(vehicle.Id, request);

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    // =========================================================================
    // 4. PERSISTENCE INTEGRITY & IMMUTABILITY
    // =========================================================================

    [Fact]
    public async Task UpdateVehicleStatus_PreservesAllOtherVehicleAttributes()
    {
        var (dbContext, _, _, controller, category, vehicle) = await CreateFixtureAsync();
        SetUserContext(controller, "FLEET_MANAGER");

        var originalVin = vehicle.Vin;
        var originalPlate = vehicle.LicensePlate;
        var originalMake = vehicle.Make;
        var originalModel = vehicle.Model;
        var originalYear = vehicle.Year;
        var originalCategoryId = vehicle.VehicleCategoryId;
        var originalRate = vehicle.DailyRate;
        var originalTransmission = vehicle.Transmission;
        var originalFuel = vehicle.FuelType;
        var originalCapacity = vehicle.SeatingCapacity;
        var originalHub = vehicle.HubLocation;
        var originalMileage = vehicle.Mileage;
        var originalCreatedAt = vehicle.CreatedAt;

        var request = new UpdateVehicleStatusRequest { Status = VehicleStatus.Maintenance };
        var result = await controller.UpdateVehicleStatus(vehicle.Id, request);

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();

        var inDb = await dbContext.Vehicles.FindAsync(vehicle.Id);
        inDb.Should().NotBeNull();
        inDb!.Status.Should().Be(VehicleStatus.Maintenance);
        inDb.Vin.Should().Be(originalVin);
        inDb.LicensePlate.Should().Be(originalPlate);
        inDb.Make.Should().Be(originalMake);
        inDb.Model.Should().Be(originalModel);
        inDb.Year.Should().Be(originalYear);
        inDb.VehicleCategoryId.Should().Be(originalCategoryId);
        inDb.DailyRate.Should().Be(originalRate);
        inDb.Transmission.Should().Be(originalTransmission);
        inDb.FuelType.Should().Be(originalFuel);
        inDb.SeatingCapacity.Should().Be(originalCapacity);
        inDb.HubLocation.Should().Be(originalHub);
        inDb.Mileage.Should().Be(originalMileage);
        inDb.CreatedAt.Should().Be(originalCreatedAt);
        inDb.UpdatedAt.Should().NotBeNull();
    }

    // =========================================================================
    // 5. RETIREMENT / DEACTIVATION & ACTIVE-FLEET EXCLUSION TESTS
    // =========================================================================

    [Fact]
    public async Task GetPagedVehicles_ExcludesRetiredVehiclesByDefault()
    {
        var (dbContext, _, vehicleService, _, category, activeVehicle) = await CreateFixtureAsync();

        var retiredVehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            Vin = "1HGCR2F83HA000099",
            LicensePlate = "WP-CAB-9999",
            Make = "Toyota",
            Model = "Prius",
            Year = 2022,
            VehicleCategoryId = category.Id,
            Category = category,
            DailyRate = 12000m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Colombo Fort Hub",
            Mileage = 85000,
            Status = VehicleStatus.Retired,
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        };
        dbContext.Vehicles.Add(retiredVehicle);
        await dbContext.SaveChangesAsync();

        // Normal query without status filter
        var result = await vehicleService.GetPagedVehiclesAsync(new VehicleQueryParameters());

        result.Items.Should().ContainSingle(v => v.Id == activeVehicle.Id);
        result.Items.Should().NotContain(v => v.Id == retiredVehicle.Id);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPagedVehicles_IncludeRetiredTrue_ReturnsBothActiveAndRetired()
    {
        var (dbContext, _, vehicleService, _, category, activeVehicle) = await CreateFixtureAsync();

        var retiredVehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            Vin = "1HGCR2F83HA000099",
            LicensePlate = "WP-CAB-9999",
            Make = "Toyota",
            Model = "Prius",
            Year = 2022,
            VehicleCategoryId = category.Id,
            Category = category,
            DailyRate = 12000m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Colombo Fort Hub",
            Mileage = 85000,
            Status = VehicleStatus.Retired,
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        };
        dbContext.Vehicles.Add(retiredVehicle);
        await dbContext.SaveChangesAsync();

        var result = await vehicleService.GetPagedVehiclesAsync(new VehicleQueryParameters { IncludeRetired = true });

        result.Items.Should().HaveCount(2);
        result.Items.Should().Contain(v => v.Id == activeVehicle.Id);
        result.Items.Should().Contain(v => v.Id == retiredVehicle.Id);
    }

    [Fact]
    public async Task GetPagedVehicles_StatusRetired_ReturnsOnlyRetiredVehicles()
    {
        var (dbContext, _, vehicleService, _, category, activeVehicle) = await CreateFixtureAsync();

        var retiredVehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            Vin = "1HGCR2F83HA000099",
            LicensePlate = "WP-CAB-9999",
            Make = "Toyota",
            Model = "Prius",
            Year = 2022,
            VehicleCategoryId = category.Id,
            Category = category,
            DailyRate = 12000m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = "Colombo Fort Hub",
            Mileage = 85000,
            Status = VehicleStatus.Retired,
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        };
        dbContext.Vehicles.Add(retiredVehicle);
        await dbContext.SaveChangesAsync();

        var result = await vehicleService.GetPagedVehiclesAsync(new VehicleQueryParameters { Status = VehicleStatus.Retired });

        result.Items.Should().ContainSingle(v => v.Id == retiredVehicle.Id);
        result.Items.Should().NotContain(v => v.Id == activeVehicle.Id);
    }

    [Fact]
    public async Task RetireVehicle_AsFleetManager_RetiresVehicleAndPreservesHistory()
    {
        var (dbContext, kafkaMock, _, controller, category, vehicle) = await CreateFixtureAsync();
        SetUserContext(controller, "FLEET_MANAGER");

        var originalVin = vehicle.Vin;
        var originalPlate = vehicle.LicensePlate;
        var originalMake = vehicle.Make;
        var originalModel = vehicle.Model;
        var originalYear = vehicle.Year;
        var originalCategoryId = vehicle.VehicleCategoryId;
        var originalRate = vehicle.DailyRate;
        var originalTransmission = vehicle.Transmission;
        var originalFuel = vehicle.FuelType;
        var originalCapacity = vehicle.SeatingCapacity;
        var originalHub = vehicle.HubLocation;
        var originalMileage = vehicle.Mileage;
        var originalCreatedAt = vehicle.CreatedAt;

        var result = await controller.RetireVehicle(vehicle.Id, "End of lease service term");

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = okResult.Value as VehicleResponse;
        response.Should().NotBeNull();
        response!.Status.Should().Be(VehicleStatus.Retired);
        response.UpdatedAt.Should().NotBeNull();

        // Verify in DB
        var inDb = await dbContext.Vehicles.FindAsync(vehicle.Id);
        inDb.Should().NotBeNull();
        inDb!.Status.Should().Be(VehicleStatus.Retired);
        inDb.Vin.Should().Be(originalVin);
        inDb.LicensePlate.Should().Be(originalPlate);
        inDb.Make.Should().Be(originalMake);
        inDb.Model.Should().Be(originalModel);
        inDb.Year.Should().Be(originalYear);
        inDb.VehicleCategoryId.Should().Be(originalCategoryId);
        inDb.DailyRate.Should().Be(originalRate);
        inDb.Transmission.Should().Be(originalTransmission);
        inDb.FuelType.Should().Be(originalFuel);
        inDb.SeatingCapacity.Should().Be(originalCapacity);
        inDb.HubLocation.Should().Be(originalHub);
        inDb.Mileage.Should().Be(originalMileage);
        inDb.CreatedAt.Should().Be(originalCreatedAt);
        inDb.UpdatedAt.Should().NotBeNull();

        // Verify Kafka event published
        kafkaMock.Verify(k => k.PublishAsync(
            "vehicle-events",
            vehicle.Id.ToString(),
            It.Is<VehicleUpdatedEvent>(e => e.VehicleId == vehicle.Id && e.Status == VehicleStatus.Retired)),
            Times.Once);
    }

    [Fact]
    public async Task RetireVehicle_VehicleInUse_Returns400BadRequest()
    {
        var (dbContext, _, _, controller, _, vehicle) = await CreateFixtureAsync();
        SetUserContext(controller, "ADMIN");

        vehicle.Status = VehicleStatus.InUse;
        await dbContext.SaveChangesAsync();

        var result = await controller.RetireVehicle(vehicle.Id, "Decommission attempt");

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        // Verify status unchanged
        var inDb = await dbContext.Vehicles.FindAsync(vehicle.Id);
        inDb!.Status.Should().Be(VehicleStatus.InUse);
    }

    [Fact]
    public async Task ReactivateVehicle_AsAdmin_RestoresStatusToAvailable()
    {
        var (dbContext, _, _, controller, _, vehicle) = await CreateFixtureAsync();
        SetUserContext(controller, "ADMIN");

        vehicle.Status = VehicleStatus.Retired;
        await dbContext.SaveChangesAsync();

        var result = await controller.ReactivateVehicle(vehicle.Id);

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        var response = okResult!.Value as VehicleResponse;
        response!.Status.Should().Be(VehicleStatus.Available);

        var inDb = await dbContext.Vehicles.FindAsync(vehicle.Id);
        inDb!.Status.Should().Be(VehicleStatus.Available);
    }

    [Fact]
    public void RetireVehicleEndpoint_HasAuthorizeRolesAttribute()
    {
        var method = typeof(VehiclesController).GetMethod(nameof(VehiclesController.RetireVehicle));
        method.Should().NotBeNull();

        var authAttr = method!.GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull();
        authAttr!.Roles.Should().Be("FLEET_MANAGER,ADMIN");
    }
}
