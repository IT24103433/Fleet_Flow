using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace FleetService.Tests;

public class MaintenanceControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public MaintenanceControllerTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("MAINTENANCE_STAFF")]
    [InlineData("FLEET_MANAGER")]
    [InlineData("ADMIN")]
    public async Task CreateRecord_AuthorizedStaffRole_ReturnsCreated(string role)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateToken(Guid.NewGuid(), role));
        var vehicle = await SeedVehicleAsync();

        var response = await client.PostAsJsonAsync("/api/maintenance/records", ValidRequest(vehicle.Id));
        var record = await response.Content.ReadFromJsonAsync<MaintenanceRecordResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(record);
        Assert.Equal(MaintenanceStatus.SCHEDULED, record.Status);
    }

    [Fact]
    public async Task MaintenanceRecordEndpoints_CustomerRoleIsForbidden()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateToken(Guid.NewGuid(), "CUSTOMER"));
        var vehicle = await SeedVehicleAsync();

        var createResponse = await client.PostAsJsonAsync("/api/maintenance/records", ValidRequest(vehicle.Id));
        var historyResponse = await client.GetAsync($"/api/maintenance/records?vehicleId={vehicle.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, historyResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_AuthorizedRolePersistsButCustomerIsForbidden()
    {
        using var client = _factory.CreateClient();
        var vehicle = await SeedVehicleAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateToken(Guid.NewGuid(), "MAINTENANCE_STAFF"));
        var createResponse = await client.PostAsJsonAsync("/api/maintenance/records", ValidRequest(vehicle.Id));
        var created = await createResponse.Content.ReadFromJsonAsync<MaintenanceRecordResponse>();
        Assert.NotNull(created);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateToken(Guid.NewGuid(), "CUSTOMER"));
        var forbiddenResponse = await client.PatchAsJsonAsync(
            $"/api/maintenance/records/{created.Id}/status",
            new UpdateMaintenanceStatusRequest { Status = MaintenanceStatus.IN_PROGRESS });

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateToken(Guid.NewGuid(), "MAINTENANCE_STAFF"));
        var successResponse = await client.PatchAsJsonAsync(
            $"/api/maintenance/records/{created.Id}/status",
            new UpdateMaintenanceStatusRequest { Status = MaintenanceStatus.IN_PROGRESS });
        var updated = await successResponse.Content.ReadFromJsonAsync<MaintenanceRecordResponse>();

        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, successResponse.StatusCode);
        Assert.Equal(MaintenanceStatus.IN_PROGRESS, updated!.Status);
    }

    [Fact]
    public async Task CreateAndHistory_InvalidRequestsReturnAppropriateErrors()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateToken(Guid.NewGuid(), "MAINTENANCE_STAFF"));

        var invalidCreate = await client.PostAsJsonAsync("/api/maintenance/records", new CreateMaintenanceRecordRequest
        {
            VehicleId = Guid.NewGuid(),
            ScheduledDateTime = DateTime.UtcNow.AddDays(-2),
            ServiceInformation = string.Empty,
            Cost = -10
        });
        var missingVehicleHistory = await client.GetAsync($"/api/maintenance/records?vehicleId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.BadRequest, invalidCreate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingVehicleHistory.StatusCode);
    }

    private async Task<Vehicle> SeedVehicleAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        var category = new VehicleCategory
        {
            Id = Guid.NewGuid(),
            Name = $"Maintenance-{Guid.NewGuid():N}",
            Description = "Maintenance controller test category"
        };
        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            Vin = Guid.NewGuid().ToString("N")[..17].ToUpperInvariant(),
            LicensePlate = $"MC-{Guid.NewGuid().ToString("N")[..8]}",
            Make = "Nissan",
            Model = "Leaf",
            Year = 2025,
            DailyRate = 15000m,
            Transmission = "Automatic",
            FuelType = "Electric",
            SeatingCapacity = "5",
            HubLocation = "Colombo Fort Hub",
            Mileage = 10000,
            Status = VehicleStatus.Available,
            VehicleCategoryId = category.Id,
            Category = category
        };
        context.AddRange(category, vehicle);
        await context.SaveChangesAsync();
        return vehicle;
    }

    private static CreateMaintenanceRecordRequest ValidRequest(Guid vehicleId)
    {
        return new CreateMaintenanceRecordRequest
        {
            VehicleId = vehicleId,
            ScheduledDateTime = DateTime.UtcNow.AddDays(2),
            ServiceInformation = "Scheduled diagnostic inspection",
            Details = "Inspect braking and electrical systems.",
            Cost = 1000m
        };
    }

    private static string GenerateToken(Guid userId, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, $"maintenance-{role.ToLowerInvariant()}"),
            new Claim(ClaimTypes.Role, role)
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("TestSigningKeyAtLeast32BytesLongSoItIsValidAndDoesNotError"));
        var token = new JwtSecurityToken(
            issuer: "FleetFlow.IdentityService",
            audience: "FleetFlow.Client",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
