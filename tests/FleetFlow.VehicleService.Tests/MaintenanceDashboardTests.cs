using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using FleetService.Api.Controllers;
using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FleetFlow.VehicleService.Tests;

public class MaintenanceDashboardTests
{
    private (FleetDbContext dbContext, MaintenanceService maintenanceService, MaintenanceController controller, VehicleCategory category) CreateFixture()
    {
        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new FleetDbContext(options);
        var category = new VehicleCategory
        {
            Id = Guid.NewGuid(),
            Name = "Standard Sedan",
            Description = "Standard sedans"
        };
        dbContext.VehicleCategories.Add(category);
        dbContext.SaveChanges();

        var maintenanceService = new MaintenanceService(dbContext);
        var controller = new MaintenanceController(maintenanceService);

        return (dbContext, maintenanceService, controller, category);
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

    private static Vehicle CreateVehicle(
        Guid categoryId,
        string vin,
        string plate,
        VehicleStatus status,
        int mileage = 10000,
        string hub = "Colombo Fort Hub",
        DateTime? createdAt = null,
        DateTime? updatedAt = null)
    {
        return new Vehicle
        {
            Id = Guid.NewGuid(),
            Vin = vin,
            LicensePlate = plate,
            Make = "Toyota",
            Model = "Axio",
            Year = 2024,
            VehicleCategoryId = categoryId,
            DailyRate = 12000m,
            Transmission = "Automatic",
            FuelType = "Hybrid",
            SeatingCapacity = "5 Passengers",
            HubLocation = hub,
            Mileage = mileage,
            Status = status,
            CreatedAt = createdAt ?? DateTime.UtcNow.AddDays(-20),
            UpdatedAt = updatedAt
        };
    }

    // =========================================================================
    // 1. ZERO FABRICATED METRICS & EMPTY DATABASE
    // =========================================================================

    [Fact]
    public async Task GetDashboard_OnEmptyDatabase_ReturnsExactZerosAndEmptyListsWithoutFabrication()
    {
        // Arrange
        var (_, maintenanceService, _, _) = CreateFixture();

        // Act
        var result = await maintenanceService.GetMaintenanceDashboardAsync();

        // Assert
        result.Should().NotBeNull();
        result.StatusCounts.TotalVehicles.Should().Be(0);
        result.StatusCounts.UndergoingMaintenance.Should().Be(0);
        result.StatusCounts.Available.Should().Be(0);
        result.StatusCounts.InUse.Should().Be(0);
        result.StatusCounts.Retired.Should().Be(0);
        result.AttentionItems.Should().BeEmpty();
        result.MaintenanceVehicles.Should().BeEmpty();
    }

    // =========================================================================
    // 2. STATUS COUNTS AGGREGATION FROM PERSISTED DATA
    // =========================================================================

    [Fact]
    public async Task GetDashboard_WithVariousStatuses_AggregatesPersistedCountsAccurately()
    {
        // Arrange
        var (dbContext, maintenanceService, _, category) = CreateFixture();

        dbContext.Vehicles.AddRange(
            CreateVehicle(category.Id, "VIN001", "WP-001", VehicleStatus.Available),
            CreateVehicle(category.Id, "VIN002", "WP-002", VehicleStatus.Available),
            CreateVehicle(category.Id, "VIN003", "WP-003", VehicleStatus.InUse),
            CreateVehicle(category.Id, "VIN004", "WP-004", VehicleStatus.Maintenance),
            CreateVehicle(category.Id, "VIN005", "WP-005", VehicleStatus.Maintenance),
            CreateVehicle(category.Id, "VIN006", "WP-006", VehicleStatus.Retired)
        );
        await dbContext.SaveChangesAsync();

        // Act
        var result = await maintenanceService.GetMaintenanceDashboardAsync();

        // Assert
        result.StatusCounts.TotalVehicles.Should().Be(6);
        result.StatusCounts.Available.Should().Be(2);
        result.StatusCounts.InUse.Should().Be(1);
        result.StatusCounts.UndergoingMaintenance.Should().Be(2);
        result.StatusCounts.Retired.Should().Be(1);
    }

    // =========================================================================
    // 3. IDENTIFYING VEHICLES REQUIRING ATTENTION
    // =========================================================================

    [Fact]
    public async Task GetDashboard_IdentifiesVehiclesInMaintenance_AsAttentionItems()
    {
        // Arrange
        var (dbContext, maintenanceService, _, category) = CreateFixture();

        var inMaintenanceVehicle = CreateVehicle(
            category.Id,
            "VIN-MAINT-1",
            "WP-MNT-01",
            VehicleStatus.Maintenance,
            mileage: 15000,
            updatedAt: DateTime.UtcNow.AddDays(-2)
        );
        var normalAvailable = CreateVehicle(category.Id, "VIN-AVAIL-1", "WP-AVL-01", VehicleStatus.Available, mileage: 12000);

        dbContext.Vehicles.AddRange(inMaintenanceVehicle, normalAvailable);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await maintenanceService.GetMaintenanceDashboardAsync();

        // Assert
        result.AttentionItems.Should().ContainSingle(a => a.Vin == "VIN-MAINT-1");
        var attentionItem = result.AttentionItems.First(a => a.Vin == "VIN-MAINT-1");
        attentionItem.Status.Should().Be(VehicleStatus.Maintenance);
        attentionItem.AttentionReason.Should().Contain("Undergoing Active Maintenance");
        attentionItem.Urgency.Should().Be("Medium");
    }

    [Fact]
    public async Task GetDashboard_IdentifiesProlongedMaintenance_WithHighUrgency()
    {
        // Arrange
        var (dbContext, maintenanceService, _, category) = CreateFixture();

        var prolongedVehicle = CreateVehicle(
            category.Id,
            "VIN-PROLONGED",
            "WP-PRL-01",
            VehicleStatus.Maintenance,
            mileage: 20000,
            updatedAt: DateTime.UtcNow.AddDays(-10) // 10 days in maintenance (> 7)
        );

        dbContext.Vehicles.Add(prolongedVehicle);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await maintenanceService.GetMaintenanceDashboardAsync();

        // Assert
        result.AttentionItems.Should().ContainSingle(a => a.Vin == "VIN-PROLONGED");
        var item = result.AttentionItems.First(a => a.Vin == "VIN-PROLONGED");
        item.Urgency.Should().Be("High");
        item.AttentionReason.Should().Contain("Extended Service");
        item.DaysInMaintenance.Should().BeGreaterThanOrEqualTo(9);
    }

    [Fact]
    public async Task GetDashboard_IdentifiesHighMileageAvailableVehicles_AsPreventiveAttentionItems()
    {
        // Arrange
        var (dbContext, maintenanceService, _, category) = CreateFixture();

        var highMileageVehicle = CreateVehicle(
            category.Id,
            "VIN-HIGH-MILEAGE",
            "WP-HML-01",
            VehicleStatus.Available,
            mileage: 85000
        );

        dbContext.Vehicles.Add(highMileageVehicle);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await maintenanceService.GetMaintenanceDashboardAsync();

        // Assert
        result.AttentionItems.Should().ContainSingle(a => a.Vin == "VIN-HIGH-MILEAGE");
        var item = result.AttentionItems.First(a => a.Vin == "VIN-HIGH-MILEAGE");
        item.AttentionReason.Should().Contain("Preventive Service Recommended");
        item.Urgency.Should().Be("High"); // >= 80000 -> High
    }

    // =========================================================================
    // 4. HUB LOCATION FILTERING
    // =========================================================================

    [Fact]
    public async Task GetDashboard_WithHubFilter_OnlyAggregatesMatchingHub()
    {
        // Arrange
        var (dbContext, maintenanceService, _, category) = CreateFixture();

        dbContext.Vehicles.AddRange(
            CreateVehicle(category.Id, "VIN-HUB-1", "WP-H1", VehicleStatus.Maintenance, hub: "Colombo Fort Hub"),
            CreateVehicle(category.Id, "VIN-HUB-2", "WP-H2", VehicleStatus.Available, hub: "Colombo Fort Hub"),
            CreateVehicle(category.Id, "VIN-HUB-3", "WP-H3", VehicleStatus.Maintenance, hub: "Kandy Central Hub")
        );
        await dbContext.SaveChangesAsync();

        // Act
        var colomboResult = await maintenanceService.GetMaintenanceDashboardAsync("Colombo Fort Hub");

        // Assert
        colomboResult.StatusCounts.TotalVehicles.Should().Be(2);
        colomboResult.StatusCounts.UndergoingMaintenance.Should().Be(1);
        colomboResult.StatusCounts.Available.Should().Be(1);
        colomboResult.MaintenanceVehicles.Should().ContainSingle(v => v.Vin == "VIN-HUB-1");
    }

    // =========================================================================
    // 5. CONTROLLER ROLE AUTHORIZATION & HTTP ENDPOINTS
    // =========================================================================

    [Fact]
    public void MaintenanceController_HasAuthorizeAttribute_RestrictingToStaffRoles()
    {
        // Act
        var authorizeAttr = typeof(MaintenanceController).GetCustomAttribute<AuthorizeAttribute>();

        // Assert
        authorizeAttr.Should().NotBeNull();
        authorizeAttr!.Roles.Should().Be("FLEET_MANAGER,ADMIN,MAINTENANCE_STAFF");
    }

    [Theory]
    [InlineData("FLEET_MANAGER")]
    [InlineData("ADMIN")]
    [InlineData("MAINTENANCE_STAFF")]
    public async Task GetDashboard_WithAuthorizedRoles_Returns200Ok(string role)
    {
        // Arrange
        var (dbContext, _, controller, category) = CreateFixture();
        SetUserContext(controller, role);

        dbContext.Vehicles.Add(CreateVehicle(category.Id, "VIN-CTRL-1", "WP-C01", VehicleStatus.Maintenance));
        await dbContext.SaveChangesAsync();

        // Act
        var actionResult = await controller.GetDashboard();

        // Assert
        var okResult = actionResult.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = okResult.Value as MaintenanceDashboardResponse;
        response.Should().NotBeNull();
        response!.StatusCounts.UndergoingMaintenance.Should().Be(1);
    }
}
