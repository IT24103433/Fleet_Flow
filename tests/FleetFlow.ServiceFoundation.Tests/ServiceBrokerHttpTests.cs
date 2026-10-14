using System.Net;
using System.Net.Http.Json;
using FleetFlow.Contracts;
using IdentityService.Api;
using IdentityService.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace FleetFlow.ServiceFoundation.Tests;
public sealed class ServiceBrokerHttpTests
{
    [Fact]public async Task BrokerModelBindingIssuesTokenAndRejectsInvalidCredentials()
    {
        using var factory=new BrokerFactory();using var client=factory.CreateClient();
        var valid=await client.PostAsJsonAsync("/internal/service-token",new ServiceTokenRequest("fleet-service",SecurityTests.Secret));
        Assert.Equal(HttpStatusCode.OK,valid.StatusCode);Assert.Equal("no-store",valid.Headers.CacheControl?.ToString());
        var token=await valid.Content.ReadFromJsonAsync<ServiceTokenResponse>();Assert.NotNull(token);Assert.Equal(300,token.ExpiresIn);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/internal/service-token",new ServiceTokenRequest("fleet-service","wrong"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/internal/service-token",new { clientId="fleet-service",clientSecret=new string('x',513) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/internal/service-token",new { clientId="",clientSecret=SecurityTests.Secret })).StatusCode);
    }
    private sealed class BrokerFactory:WebApplicationFactory<IdentityApplication>
    {
        private readonly string name=Guid.NewGuid().ToString();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");builder.UseSetting("Jwt:Key",SecurityTests.UserKey);
            builder.UseSetting("SERVICE_AUTH_ENABLED","true");builder.UseSetting("SERVICE_AUTH_SIGNING_KEY",SecurityTests.InternalKey);
            builder.UseSetting("ServiceAuth:Clients:fleet-service",SecurityTests.Secret);
            builder.ConfigureServices(services=>
            {
                foreach(var d in services.Where(d=>d.ServiceType==typeof(IdentityDbContext)||d.ServiceType.FullName?.Contains("EntityFrameworkCore")==true||d.ServiceType.FullName?.Contains("Npgsql")==true).ToList())services.Remove(d);
                services.AddDbContext<IdentityDbContext>(o=>o.UseInMemoryDatabase(name));
            });
        }
    }
}
