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
using Microsoft.EntityFrameworkCore;
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
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.NotNull(booking.Vehicle);
        Assert.Equal(vehicle.LicensePlate, booking.Vehicle.LicensePlate);
        Assert.Equal(600.00m, booking.TotalCost); // 3 days * 200.00

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        var persisted = await context.Bookings.SingleAsync(b => b.Id == booking.Id);
        Assert.Equal(userId, persisted.CustomerId);
        Assert.Equal(BookingStatus.Confirmed, persisted.Status);
    }

    [Fact]
    public async Task CreateBooking_WithNonCustomerRole_Returns403Forbidden()
    {
        var client = _factory.CreateClient();
        var token = GenerateToken(Guid.NewGuid(), "fleet_manager", "manager@example.com", "FLEET_MANAGER");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var vehicle = await SeedVehicleAsync();

        var response = await client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = DateTime.UtcNow.AddDays(5),
            EndDateTime = DateTime.UtcNow.AddDays(7)
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_ClientSuppliedCancelledStatus_IsIgnoredAndServerConfirmsBooking()
    {
        var client = _factory.CreateClient();
        var userId = Guid.NewGuid();
        var token = GenerateToken(userId, "status_customer", "status@example.com", "CUSTOMER");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var vehicle = await SeedVehicleAsync();

        var response = await client.PostAsJsonAsync("/api/bookings", new
        {
            vehicleId = vehicle.Id,
            startDateTime = DateTime.UtcNow.AddDays(8),
            endDateTime = DateTime.UtcNow.AddDays(10),
            status = "Cancelled"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booking = await response.Content.ReadFromJsonAsync<BookingResponse>();
        Assert.NotNull(booking);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
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
        Assert.All(bookings, b => Assert.Equal(userId, b.CustomerId));
    }

    [Fact]
    public async Task GetMyBookings_VehicleQueryCannotExposeAnotherCustomersBooking()
    {
        var client = _factory.CreateClient();
        var firstCustomerId = Guid.NewGuid();
        var secondCustomerId = Guid.NewGuid();
        var vehicle = await SeedVehicleAsync();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateToken(firstCustomerId, "first_customer", "first@example.com", "CUSTOMER"));
        var firstCreateResponse = await client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = DateTime.UtcNow.AddDays(70),
            EndDateTime = DateTime.UtcNow.AddDays(72)
        });
        Assert.Equal(HttpStatusCode.Created, firstCreateResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateToken(secondCustomerId, "second_customer", "second@example.com", "CUSTOMER"));
        var secondCreateResponse = await client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = DateTime.UtcNow.AddDays(75),
            EndDateTime = DateTime.UtcNow.AddDays(77)
        });
        Assert.Equal(HttpStatusCode.Created, secondCreateResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateToken(firstCustomerId, "first_customer", "first@example.com", "CUSTOMER"));
        var response = await client.GetAsync($"/api/bookings?vehicleId={vehicle.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bookings = await response.Content.ReadFromJsonAsync<List<BookingResponse>>();
        Assert.NotNull(bookings);
        Assert.Contains(bookings, booking => booking.CustomerId == firstCustomerId);
        Assert.DoesNotContain(bookings, booking => booking.CustomerId == secondCustomerId);
    }

    [Fact]
    public async Task GetBookingById_AnotherCustomer_Returns404NotFound()
    {
        var client = _factory.CreateClient();
        var ownerId = Guid.NewGuid();
        var ownerToken = GenerateToken(ownerId, "booking_owner", "owner@example.com", "CUSTOMER");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var vehicle = await SeedVehicleAsync();

        var createResponse = await client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = DateTime.UtcNow.AddDays(35),
            EndDateTime = DateTime.UtcNow.AddDays(37)
        });
        var created = await createResponse.Content.ReadFromJsonAsync<BookingResponse>();
        Assert.NotNull(created);

        var otherToken = GenerateToken(Guid.NewGuid(), "other_customer", "other@example.com", "CUSTOMER");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        var response = await client.GetAsync($"/api/bookings/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBookingById_AdminRole_CanAccessBooking()
    {
        var client = _factory.CreateClient();
        var ownerId = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateToken(ownerId, "admin_read_owner", "admin-owner@example.com", "CUSTOMER"));
        var vehicle = await SeedVehicleAsync();
        var createResponse = await client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = DateTime.UtcNow.AddDays(40),
            EndDateTime = DateTime.UtcNow.AddDays(42)
        });
        var created = await createResponse.Content.ReadFromJsonAsync<BookingResponse>();
        Assert.NotNull(created);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateToken(Guid.NewGuid(), "admin_reader", "admin@example.com", "ADMIN"));
        var response = await client.GetAsync($"/api/bookings/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CancelBooking_OwnerCancels_PersistsHistoryReleasesPeriodAndRejectsRepeat()
    {
        var client = _factory.CreateClient();
        var customerId = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateToken(customerId, "cancel_owner", "cancel@example.com", "CUSTOMER"));
        var vehicle = await SeedVehicleAsync();
        var start = DateTime.UtcNow.AddDays(45);
        var end = start.AddDays(2);

        var createResponse = await client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = start,
            EndDateTime = end
        });
        var created = await createResponse.Content.ReadFromJsonAsync<BookingResponse>();
        Assert.NotNull(created);

        var cancelResponse = await client.PostAsync($"/api/bookings/{created.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<BookingResponse>();
        Assert.NotNull(cancelled);
        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);

        var historyResponse = await client.GetAsync("/api/bookings");
        var history = await historyResponse.Content.ReadFromJsonAsync<List<BookingResponse>>();
        Assert.NotNull(history);
        Assert.Contains(history, b => b.Id == created.Id && b.Status == BookingStatus.Cancelled);

        var availabilityUrl = $"/api/bookings/check-availability?vehicleId={vehicle.Id}&startDateTime={Uri.EscapeDataString(start.ToString("O"))}&endDateTime={Uri.EscapeDataString(end.ToString("O"))}";
        var availabilityResponse = await client.GetAsync(availabilityUrl);
        Assert.Equal(HttpStatusCode.OK, availabilityResponse.StatusCode);

        var repeatResponse = await client.PostAsync($"/api/bookings/{created.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, repeatResponse.StatusCode);
    }

    [Fact]
    public async Task CancelBooking_AnotherCustomer_Returns404AndPreservesBooking()
    {
        var client = _factory.CreateClient();
        var ownerId = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateToken(ownerId, "protected_owner", "protected@example.com", "CUSTOMER"));
        var vehicle = await SeedVehicleAsync();
        var createResponse = await client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            VehicleId = vehicle.Id,
            StartDateTime = DateTime.UtcNow.AddDays(50),
            EndDateTime = DateTime.UtcNow.AddDays(52)
        });
        var created = await createResponse.Content.ReadFromJsonAsync<BookingResponse>();
        Assert.NotNull(created);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            GenerateToken(Guid.NewGuid(), "cancel_intruder", "intruder@example.com", "CUSTOMER"));
        var response = await client.PostAsync($"/api/bookings/{created.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        var persisted = await context.Bookings.SingleAsync(b => b.Id == created.Id);
        Assert.Equal(BookingStatus.Confirmed, persisted.Status);
    }

    [Fact]
    public async Task CheckAvailability_InvalidRangeMissingVehicleAndMaintenanceVehicle_ReturnExpectedErrors()
    {
        var client = _factory.CreateClient();
        var start = DateTime.UtcNow.AddDays(3);
        var invalidRangeUrl = $"/api/bookings/check-availability?vehicleId={Guid.NewGuid()}&startDateTime={Uri.EscapeDataString(start.ToString("O"))}&endDateTime={Uri.EscapeDataString(start.AddHours(-1).ToString("O"))}";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(invalidRangeUrl)).StatusCode);

        var missingVehicleUrl = $"/api/bookings/check-availability?vehicleId={Guid.NewGuid()}&startDateTime={Uri.EscapeDataString(start.ToString("O"))}&endDateTime={Uri.EscapeDataString(start.AddDays(1).ToString("O"))}";
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(missingVehicleUrl)).StatusCode);

        var maintenanceVehicle = await SeedVehicleAsync(VehicleStatus.Maintenance);
        var maintenanceUrl = $"/api/bookings/check-availability?vehicleId={maintenanceVehicle.Id}&startDateTime={Uri.EscapeDataString(start.ToString("O"))}&endDateTime={Uri.EscapeDataString(start.AddDays(1).ToString("O"))}";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(maintenanceUrl)).StatusCode);
    }
}
