using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FleetFlow.Contracts;
using FleetFlow.ServiceDefaults;
using FleetService.Api.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using NotificationService.Api;
using NotificationService.Api.Data;
namespace NotificationService.Tests;
public sealed class NotificationApiTests
{
    [Fact]public async Task InternalDeliveryBrowserInboxAndOwnershipPreserveContracts()
    {
        using var factory=new NotificationFactory();var item=InboxTests.Event();using var browser=factory.Browser(item.CustomerId);
        Assert.Equal(HttpStatusCode.Unauthorized,(await browser.PostAsJsonAsync("/internal/notifications/events",item)).StatusCode);
        using var service=factory.Internal("fleet-service");
        Assert.Equal(HttpStatusCode.OK,(await service.PostAsJsonAsync("/internal/notifications/events",item)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await service.PostAsJsonAsync("/internal/notifications/events",item)).StatusCode);
        var row=Assert.Single((await browser.GetFromJsonAsync<NotificationResponse[]>("/api/notifications"))!);
        Assert.Equal(HttpStatusCode.NoContent,(await browser.PatchAsync($"/api/notifications/{row.Id}/read",null)).StatusCode);
        using var other=factory.Browser(Guid.NewGuid(),"ADMIN");
        Assert.Empty((await other.GetFromJsonAsync<NotificationResponse[]>("/api/notifications"))!);
        Assert.Equal(HttpStatusCode.NotFound,(await other.PatchAsync($"/api/notifications/{row.Id}/read",null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await service.GetAsync("/api/notifications")).StatusCode);
        Assert.True(Assert.Single((await browser.GetFromJsonAsync<NotificationResponse[]>("/api/notifications"))!).IsRead);
    }
    [Theory][InlineData("CUSTOMER",HttpStatusCode.Forbidden)][InlineData("FLEET_MANAGER",HttpStatusCode.Forbidden)][InlineData("ADMIN",HttpStatusCode.Created)]
    public async Task SystemNotificationCreationRemainsAdminOnly(string role,HttpStatusCode expected)
    {
        using var factory=new NotificationFactory();using var client=factory.Browser(Guid.NewGuid(),role);
        Assert.Equal(expected,(await client.PostAsJsonAsync("/api/notifications/system",new CreateSystemNotificationRequest
        {TargetUserId=Guid.NewGuid(),Title="Notice",Message="Update"})).StatusCode);
    }
    [Fact]public async Task WrongScopeForcedPasswordAndInactiveServiceAreRejected()
    {
        using var factory=new NotificationFactory();using var wrong=factory.Internal("notification-service");
        Assert.Equal(HttpStatusCode.Forbidden,(await wrong.PostAsJsonAsync("/internal/notifications/events",InboxTests.Event())).StatusCode);
        using var forced=factory.Browser(Guid.NewGuid(),"ADMIN",true);
        Assert.Equal(HttpStatusCode.Forbidden,(await forced.GetAsync("/api/notifications")).StatusCode);
        using var inactive=new NotificationFactory(false);using var browser=inactive.Browser(Guid.NewGuid());
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await browser.GetAsync("/api/notifications")).StatusCode);
    }
}
public sealed class NotificationFactory(bool active=true):WebApplicationFactory<NotificationApplication>
{
    public const string UserKey="notification-tests-user-key-123456789012345678";
    public const string InternalKey="notification-tests-service-key-123456789012345678";
    public const string Secret="notification-tests-client-secret-123456789012345";
    private readonly string database=Guid.NewGuid().ToString();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");builder.UseSetting("Jwt:Key",UserKey);
        builder.UseSetting("SERVICE_AUTH_ENABLED","true");builder.UseSetting("SERVICE_AUTH_SIGNING_KEY",InternalKey);
        foreach(var id in ServiceScopes.ClientGrants.Keys)builder.UseSetting($"ServiceAuth:Clients:{id}",Secret);
        builder.UseSetting("NOTIFICATION_SERVICE_ACTIVE",active.ToString());builder.UseSetting("NOTIFICATION_MIGRATION_ID",Guid.NewGuid().ToString());
        builder.ConfigureServices(services=>
        {
            foreach(var d in services.Where(d=>d.ServiceType==typeof(NotificationDbContext)||d.ServiceType.FullName?.Contains("EntityFrameworkCore")==true||d.ServiceType.FullName?.Contains("Npgsql")==true).ToList())services.Remove(d);
            services.AddDbContext<NotificationDbContext>(o=>o.UseInMemoryDatabase(database));
            services.RemoveAll<IDatabaseReadiness>();services.AddSingleton<IDatabaseReadiness>(new Ready());services.RemoveAll<IAccountAccessReader>();services.AddSingleton<IAccountAccessReader>(new ContractAccounts());
        });
    }
    public HttpClient Browser(Guid user,string role="CUSTOMER",bool forced=false)
    {
        var claims=new[]{new Claim("sub",user.ToString()),new Claim("access_version","0"),new Claim(ClaimTypes.Role,role),new Claim("must_change_password",forced.ToString().ToLowerInvariant())};
        var token=new JwtSecurityToken("FleetFlow.IdentityService","FleetFlow.Client",claims,DateTime.UtcNow.AddSeconds(-1),DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(UserKey)),SecurityAlgorithms.HmacSha256));
        var client=CreateClient();client.DefaultRequestHeaders.Authorization=new("Bearer",new JwtSecurityTokenHandler().WriteToken(token));return client;
    }
    public HttpClient Internal(string id)
    {
        var options=new ServiceAuthenticationOptions(true,InternalKey,"FleetFlow.IdentityService",ServiceScopes.ClientGrants.Keys.ToDictionary(k=>k,_=>Secret));
        var client=CreateClient();client.DefaultRequestHeaders.Authorization=new("Bearer",new ServiceTokenIssuer(options,TimeProvider.System).Issue(new(id,Secret))!.AccessToken);return client;
    }
    private sealed class Ready:IDatabaseReadiness{public Task<bool> IsReadyAsync(CancellationToken ct)=>Task.FromResult(true);}
}

// Synthetic current accounts isolate these existing ownership/role contract tests.
internal sealed class ContractAccounts : IAccountAccessReader { public Task<AccountAccessState?> ReadAsync(Guid id,CancellationToken ct)=>Task.FromResult<AccountAccessState?>(new(id,0,true,false)); }
