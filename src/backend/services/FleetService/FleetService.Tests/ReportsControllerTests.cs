using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FleetService.Api.Controllers;
using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace FleetService.Tests;

public class ReportsControllerTests : IClassFixture<CustomWebApplicationFactory<ReportsController>>
{
    private readonly CustomWebApplicationFactory<ReportsController> factory;
    public ReportsControllerTests(CustomWebApplicationFactory<ReportsController> factory) => this.factory = factory;
    private static readonly string[] Endpoints = ["fleet-summary", "bookings", "maintenance", "operational-statistics"];

    private static HttpClient Client(CustomWebApplicationFactory<ReportsController> app, string? role)
    {
        var client = app.CreateClient();
        if (role == null) return client;
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role) };
        var token = new JwtSecurityToken("FleetFlow.IdentityService", "FleetFlow.Client", claims,
            expires: DateTime.UtcNow.AddHours(1), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("TestSigningKeyAtLeast32BytesLongSoItIsValidAndDoesNotError")), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    public static IEnumerable<object[]> AccessCases()
    {
        foreach (var endpoint in Endpoints)
            foreach (var role in new string?[] { null, "CUSTOMER", "ADMIN", "FLEET_MANAGER", "MAINTENANCE_STAFF" })
                yield return new object[] { endpoint, role!, role == null ? 401 :
                    role == "ADMIN" || endpoint == "maintenance" && role is "FLEET_MANAGER" or "MAINTENANCE_STAFF" ? 200 : 403 };
    }

    [Theory, MemberData(nameof(AccessCases))]
    public async Task ReportAccess_EnforcesRoleMatrix(string endpoint, string? role, int status)
    {
        using var client = Client(factory, role);
        var response = await client.GetAsync($"api/reports/{endpoint}");
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        if (status == 200) Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task InvalidJwtCannotAccessAdminReports()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "invalid-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("api/reports/fleet-summary")).StatusCode);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=2147483647&pageSize=100")]
    [InlineData("status=Unknown")]
    [InlineData("status=99")]
    public async Task InvalidBookingFiltersReturn400(string query)
    {
        using var client = Client(factory, "ADMIN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"api/reports/bookings?{query}")).StatusCode);
    }

    [Fact]
    public async Task EmptyReportsExposeZerosAndUnavailableWorkOrders()
    {
        using var isolated = new CustomWebApplicationFactory<ReportsController>();
        using var client = Client(isolated, "ADMIN");
        var fleet = (await client.GetFromJsonAsync<FleetSummaryResponse>("api/reports/fleet-summary"))!;
        Assert.Equal(0, fleet.Vehicles.Total);
        Assert.Null(fleet.CurrentBookingUtilizationPercent);
        var bookings = (await client.GetFromJsonAsync<BookingReportResponse>("api/reports/bookings"))!;
        Assert.Empty(bookings.Items);
        var maintenance = (await client.GetFromJsonAsync<MaintenanceReportResponse>("api/reports/maintenance"))!;
        Assert.Empty(maintenance.CurrentVehicles);
        Assert.False(maintenance.WorkOrders.Available);
        var stats = (await client.GetFromJsonAsync<OperationalStatisticsResponse>("api/reports/operational-statistics"))!;
        Assert.Equal(0, stats.Bookings.Total);
        Assert.Null(stats.Maintenance.WorkOrderCount);
    }

    [Fact]
    public async Task PersistedCancelledBookingAndMaintenanceVehicleAppearInAdminReports()
    {
        using var isolated = new CustomWebApplicationFactory<ReportsController>();
        using var client = Client(isolated, "ADMIN");
        var vehicleId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        using (var scope = isolated.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
            db.Vehicles.Add(new Vehicle { Id = vehicleId, LicensePlate = "WP-TEST", Make = "Test", Model = "Car", Status = VehicleStatus.Maintenance });
            db.Bookings.Add(new Booking { Id = bookingId, VehicleId = vehicleId, CustomerId = customerId, Status = BookingStatus.Cancelled,
                StartDateTime = DateTime.UtcNow, EndDateTime = DateTime.UtcNow.AddDays(1), TotalCost = 123.45m });
            await db.SaveChangesAsync();
        }
        using var json = JsonDocument.Parse(await client.GetStringAsync("api/reports/bookings?status=Cancelled"));
        var row = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("Cancelled", row.GetProperty("status").GetString());
        Assert.Equal(bookingId, row.GetProperty("bookingId").GetGuid());
        Assert.Equal(customerId, row.GetProperty("customerId").GetGuid());
        Assert.Equal("WP-TEST", row.GetProperty("licensePlate").GetString());
        Assert.False(row.TryGetProperty("customerEmail", out _));
        Assert.Equal(vehicleId, Assert.Single((await client.GetFromJsonAsync<MaintenanceReportResponse>("api/reports/maintenance"))!.CurrentVehicles).VehicleId);
        Assert.Equal(1, (await client.GetFromJsonAsync<FleetSummaryResponse>("api/reports/fleet-summary"))!.Vehicles.Total);
        // Kafka is disabled in the test host; reporting still reads database state correctly.
    }

    private sealed class UnavailableReports : IReportingService
    {
        public Task<FleetSummaryResponse> GetFleetSummaryAsync(CancellationToken ct = default) => throw new TimeoutException("private database details");
        public Task<BookingReportResponse> GetBookingReportAsync(BookingReportQuery query, CancellationToken ct = default) => throw new TimeoutException("private database details");
        public Task<MaintenanceReportResponse> GetMaintenanceReportAsync(CancellationToken ct = default) => throw new TimeoutException("private database details");
        public Task<OperationalStatisticsResponse> GetOperationalStatisticsAsync(CancellationToken ct = default) => throw new TimeoutException("private database details");
    }

    [Theory]
    [InlineData("fleet-summary")]
    [InlineData("bookings")]
    [InlineData("maintenance")]
    [InlineData("operational-statistics")]
    public async Task UnavailableDatabase_Returns503InsteadOfFabricatedZeros(string endpoint)
    {
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IReportingService>();
            services.AddSingleton<IReportingService, UnavailableReports>();
        }));
        using var client = app.CreateClient();
        using var authorized = Client(factory, "ADMIN");
        client.DefaultRequestHeaders.Authorization = authorized.DefaultRequestHeaders.Authorization;
        var response = await client.GetAsync($"api/reports/{endpoint}");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private database details", payload);
    }
}
