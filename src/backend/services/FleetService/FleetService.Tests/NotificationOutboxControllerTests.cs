using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FleetService.Api.Data;
using FleetService.Api.Entities;
using FleetService.Api.Messaging.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
namespace FleetService.Tests;
public sealed class NotificationOutboxControllerTests:IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> factory;
    public NotificationOutboxControllerTests(CustomWebApplicationFactory<Program> factory)=>this.factory=factory;
    private HttpClient Client(string role,Guid user)
    {
        var jwt=new JwtSecurityToken("FleetFlow.IdentityService","FleetFlow.Client",[new Claim(ClaimTypes.Role,role),new Claim("access_version", "0"), new Claim("sub",user.ToString())],
            expires:DateTime.UtcNow.AddMinutes(5),signingCredentials:new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes("TestSigningKeyAtLeast32BytesLongSoItIsValidAndDoesNotError")),SecurityAlgorithms.HmacSha256));
        var client=factory.CreateClient();client.DefaultRequestHeaders.Authorization=new("Bearer",new JwtSecurityTokenHandler().WriteToken(jwt));return client;
    }
    [Theory][InlineData("CUSTOMER")][InlineData("FLEET_MANAGER")][InlineData("MAINTENANCE_STAFF")]
    public async Task NonAdministratorsCannotInspectOrReplay(string role)
    {
        using var client=Client(role,Guid.NewGuid());Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/notifications/outbox")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsync($"/api/notifications/outbox/{Guid.NewGuid()}/replay",null)).StatusCode);
    }
    [Fact]public async Task AdminReplayOnlyResetsFailedEventAndRecordsActor()
    {
        var actor=Guid.NewGuid();Guid id;
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<FleetDbContext>();var row=NotificationOutbox.From(new BookingCreatedEvent
            {EventId=Guid.NewGuid(),OccurredAt=DateTime.UtcNow,CustomerId=Guid.NewGuid(),BookingId=Guid.NewGuid(),VehicleId=Guid.NewGuid()});
            row.Status="Failed";row.Attempts=8;row.InboxDeliveredAt=DateTime.UtcNow;row.KafkaPublishedAt=DateTime.UtcNow;id=row.Id;
            db.NotificationOutbox.Add(row);await db.SaveChangesAsync();
        }
        using var client=Client("ADMIN",actor);
        Assert.Equal(HttpStatusCode.Accepted,(await client.PostAsync($"/api/notifications/outbox/{id}/replay",null)).StatusCode);
        using(var scope=factory.Services.CreateScope())
        {
            var row=await scope.ServiceProvider.GetRequiredService<FleetDbContext>().NotificationOutbox.FindAsync(id);
            Assert.Equal(actor,row!.LastReplayedByUserId);Assert.Equal(1,row.ReplayCount);Assert.NotNull(row.InboxDeliveredAt);Assert.NotNull(row.KafkaPublishedAt);
        }
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsync($"/api/notifications/outbox/{id}/replay",null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsync($"/api/notifications/outbox/{Guid.NewGuid()}/replay",null)).StatusCode);
    }
}
