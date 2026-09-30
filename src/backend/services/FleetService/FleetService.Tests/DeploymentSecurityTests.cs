using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using FleetService.Api.Controllers;
using FleetService.Api.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace FleetService.Tests;

public class DeploymentSecurityTests : IClassFixture<CustomWebApplicationFactory<NotificationsController>>
{
    private readonly CustomWebApplicationFactory<NotificationsController> factory;
    public DeploymentSecurityTests(CustomWebApplicationFactory<NotificationsController> factory) => this.factory = factory;

    private HttpClient Client(string role, bool mustChange)
    {
        var client = factory.CreateClient();
        var token = new JwtSecurityToken("FleetFlow.IdentityService", "FleetFlow.Client",
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, role),
             new Claim("must_change_password", mustChange ? "true" : "false")],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("TestSigningKeyAtLeast32BytesLongSoItIsValidAndDoesNotError")), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    [Theory]
    [InlineData("CUSTOMER", "/api/bookings")]
    [InlineData("ADMIN", "/api/reports/fleet-summary")]
    [InlineData("FLEET_MANAGER", "/api/maintenance/dashboard")]
    [InlineData("MAINTENANCE_STAFF", "/api/maintenance/dashboard")]
    [InlineData("CUSTOMER", "/api/notifications")]
    public async Task ForcedPasswordChange_BlocksProtectedFleetEndpoints(string role, string path)
    {
        using var client = Client(role, true);
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("\"mustChangePassword\":true", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/api/vehicles")]
    public async Task ForcedPasswordChange_PreservesPublicEndpoints(string path)
    {
        using var client = Client("CUSTOMER", true);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task ClearedClaim_AllowsAuthorizedAccess()
    {
        using var client = Client("CUSTOMER", false);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/notifications")).StatusCode);
    }

    [Fact]
    public async Task Readiness_UninitializedSchema_Returns503_WhileLivenessSucceeds()
    {
        // InMemory cannot execute startup PostgreSQL DDL, so initialization is deliberately unsuccessful.
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }

    [Fact]
    public async Task Readiness_RequiresInitialization_EvenWhenDatabaseIsReachable()
    {
        await using var db = new FleetDbContext(new DbContextOptionsBuilder<FleetDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var readiness = new FleetReadiness();
        Assert.Equal(503, ((IStatusCodeHttpResult)await readiness.CheckAsync(db, NullLogger<FleetReadiness>.Instance)).StatusCode);
        readiness.MarkInitialized();
        Assert.Equal(200, ((IStatusCodeHttpResult)await readiness.CheckAsync(db, NullLogger<FleetReadiness>.Instance)).StatusCode);
    }

    [Fact]
    public async Task Readiness_InitializedButDisconnected_Returns503()
    {
        await using var db = new FleetDbContext(new DbContextOptionsBuilder<FleetDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=fleetflow_hardening_unreachable;Username=test;Password=test;Timeout=1;Pooling=false").Options);
        var readiness = new FleetReadiness();
        readiness.MarkInitialized();
        Assert.Equal(503, ((IStatusCodeHttpResult)await readiness.CheckAsync(db, NullLogger<FleetReadiness>.Instance)).StatusCode);
    }
}
