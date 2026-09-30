using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using FleetService.Api.Controllers;
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

}
