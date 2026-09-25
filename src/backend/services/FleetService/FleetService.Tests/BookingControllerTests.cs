using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using FleetService.Api.Controllers;
using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using Xunit;

namespace FleetService.Tests;

public class BookingControllerTests : IClassFixture<CustomWebApplicationFactory<BookingsController>>
{
    private readonly CustomWebApplicationFactory<BookingsController> _factory;
    private const string JwtKey = "TestSigningKeyAtLeast32BytesLongSoItIsValidAndDoesNotError";
    private const string Issuer = "FleetFlow.IdentityService";
    private const string Audience = "FleetFlow.Client";

    public BookingControllerTests(CustomWebApplicationFactory<BookingsController> factory)
    {
        _factory = factory;
    }

    private static string GenerateToken(Guid userId, string username, string email, string role)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<Vehicle> SeedVehicleAsync(VehicleStatus status = VehicleStatus.Available)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

        var category = new VehicleCategory
        {
            Id = Guid.NewGuid(),
            Name = "Luxury SUV",
            Description = "Full Size SUV"
        };
        context.VehicleCategories.Add(category);

        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            Vin = $"VIN{Guid.NewGuid().ToString("N")[..13]}",
            LicensePlate = $"PLT-{Guid.NewGuid().ToString("N")[..5]}",
            Make = "Audi",
            Model = "Q7",
            Year = 2024,
            DailyRate = 200.00m,
            Transmission = "Automatic",
            FuelType = "Gasoline",
            SeatingCapacity = "7",
            HubLocation = "Airport Hub",
            Mileage = 12000,
            Status = status,
            VehicleCategoryId = category.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        return vehicle;
    }

    [Fact]
    public async Task CreateBooking_WithoutToken_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var vehicle = await SeedVehicleAsync();

        var request = new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(3)
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/bookings", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_WithValidToken_Returns201Created()
    {
        // Arrange
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = GenerateToken(userId, "john_customer", "john@example.com", "CUSTOMER");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var vehicle = await SeedVehicleAsync();
        var request = new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = DateTime.UtcNow.AddDays(10),
            EndDateTime = DateTime.UtcNow.AddDays(13)
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/bookings", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var booking = await response.Content.ReadFromJsonAsync<BookingResponse>();
        Assert.NotNull(booking);
        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(userId, booking.CustomerId);
        Assert.Equal(vehicle.Id, booking.VehicleId);
        Assert.Equal(600.00m, booking.TotalCost); // 3 days * 200.00
    }

    [Fact]
    public async Task CreateBooking_WithInvalidDates_Returns400BadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = GenerateToken(userId, "john_customer", "john@example.com", "CUSTOMER");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var vehicle = await SeedVehicleAsync();
        var start = DateTime.UtcNow.AddDays(10);

        // EndDateTime <= StartDateTime
        var request = new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = start,
            EndDateTime = start.AddHours(-1)
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/bookings", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_WithOverlappingDates_Returns409Conflict()
    {
        // Arrange
        var client = _factory.CreateClient();
        var user1 = Guid.NewGuid();
        var token1 = GenerateToken(user1, "customer1", "cust1@example.com", "CUSTOMER");

        var user2 = Guid.NewGuid();
        var token2 = GenerateToken(user2, "customer2", "cust2@example.com", "CUSTOMER");

        var vehicle = await SeedVehicleAsync();
        var start = DateTime.UtcNow.AddDays(20);
        var end = DateTime.UtcNow.AddDays(25);

        var request1 = new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = start,
            EndDateTime = end
        };

        // First booking succeed
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token1);
        var response1 = await client.PostAsJsonAsync("/api/bookings", request1);
        Assert.Equal(HttpStatusCode.Created, response1.StatusCode);

        // Second booking overlaps: Day 22 to Day 27
        var request2 = new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = start.AddDays(2),
            EndDateTime = end.AddDays(2)
        };

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);
        var response2 = await client.PostAsJsonAsync("/api/bookings", request2);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);
    }

    [Fact]
    public async Task GetMyBookings_WithToken_Returns200OkWithBookingsList()
    {
        // Arrange
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = GenerateToken(userId, "john_customer", "john@example.com", "CUSTOMER");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var vehicle = await SeedVehicleAsync();
        var request = new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = DateTime.UtcNow.AddDays(30),
            EndDateTime = DateTime.UtcNow.AddDays(32)
        };

        await client.PostAsJsonAsync("/api/bookings", request);

        // Act
        var response = await client.GetAsync("/api/bookings");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bookings = await response.Content.ReadFromJsonAsync<List<BookingResponse>>();
        Assert.NotNull(bookings);
        Assert.NotEmpty(bookings);
        Assert.Contains(bookings, b => b.CustomerId == userId);
    }
}
