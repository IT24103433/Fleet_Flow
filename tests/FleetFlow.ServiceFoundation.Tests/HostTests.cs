using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FleetFlow.Contracts;
using FleetFlow.ServiceDefaults;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using RentalService.Api;
using NotificationService.Api;

namespace FleetFlow.ServiceFoundation.Tests;

public sealed class HostTests
{
    [Theory][InlineData(true)][InlineData(false)]
    public async Task HostsSeparateInternalAndBrowserAuthentication(bool rental)
    {
        if(rental) await VerifyHost<RentalApplication>("RentalService","fleet-service","rental-service");
        else await VerifyHost<NotificationApplication>("NotificationService","notification-service","fleet-service");
    }
    private static async Task VerifyHost<T>(string name,string allowedClient,string deniedClient) where T:class
    {
        using var factory=new FoundationFactory<T>(true);
        using var client=factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/service")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/internal/service")).StatusCode);
        client.DefaultRequestHeaders.Authorization=new("Bearer",BrowserToken());
        var info=await client.GetFromJsonAsync<ServiceInformation>("/api/service");
        Assert.Equal(name,info?.Service); Assert.False(info?.BusinessActivated);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/internal/service")).StatusCode);
        client.DefaultRequestHeaders.Authorization=new("Bearer",InternalToken(allowedClient));
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/internal/service")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/service")).StatusCode);
        client.DefaultRequestHeaders.Authorization=new("Bearer",InternalToken(deniedClient));
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/internal/service")).StatusCode);
        // Foundation must not claim booking/inbox ownership before extraction.
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/bookings")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/notifications")).StatusCode);
    }
    [Theory][InlineData(true,true)][InlineData(true,false)][InlineData(false,true)][InlineData(false,false)]
    public async Task LivenessDoesNotDependOnReadiness(bool rental,bool ready)
    {
        if(rental) await VerifyHealth<RentalApplication>(ready); else await VerifyHealth<NotificationApplication>(ready);
    }
    private static async Task VerifyHealth<T>(bool ready) where T:class
    {
        using var factory=new FoundationFactory<T>(ready);
        using var client=factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/health/live")).StatusCode);
        foreach(var route in new[]{"/health","/health/ready"})
        {
            var response=await client.GetAsync(route);
            Assert.Equal(ready?HttpStatusCode.OK:HttpStatusCode.ServiceUnavailable,response.StatusCode);
            Assert.DoesNotContain(SecurityTests.Secret,await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/openapi/v1.json")).StatusCode);
    }
    internal static string InternalToken(string client)=>new ServiceTokenIssuer(SecurityTests.Options(),TimeProvider.System)
        .Issue(new(client,SecurityTests.Secret))!.AccessToken;
    internal static string BrowserToken()=>new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        "FleetFlow.IdentityService","FleetFlow.Client",[new Claim("sub",Guid.NewGuid().ToString()),new Claim("role","ADMIN")],
        DateTime.UtcNow.AddSeconds(-1),DateTime.UtcNow.AddMinutes(5),new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecurityTests.UserKey)),SecurityAlgorithms.HmacSha256)));
}

internal sealed class FoundationFactory<T>(bool ready):WebApplicationFactory<T> where T:class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Jwt:Key",SecurityTests.UserKey);
        builder.UseSetting("Jwt:Issuer","FleetFlow.IdentityService");
        builder.UseSetting("Jwt:Audience","FleetFlow.Client");
        builder.UseSetting("SERVICE_AUTH_ENABLED","true");
        builder.UseSetting("SERVICE_AUTH_SIGNING_KEY",SecurityTests.InternalKey);
        foreach(var id in ServiceScopes.ClientGrants.Keys) builder.UseSetting($"ServiceAuth:Clients:{id}",SecurityTests.Secret);
        builder.ConfigureServices(services=>
        {
            services.RemoveAll<IDatabaseReadiness>();
            services.AddSingleton<IDatabaseReadiness>(new Probe(ready));
        });
    }
    private sealed class Probe(bool ready):IDatabaseReadiness
    {
        public Task<bool> IsReadyAsync(CancellationToken cancellationToken)=>Task.FromResult(ready);
    }
}
