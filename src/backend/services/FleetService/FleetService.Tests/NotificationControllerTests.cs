using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FleetService.Api.Controllers;
using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace FleetService.Tests;

public class NotificationControllerTests : IClassFixture<CustomWebApplicationFactory<NotificationsController>>
{
    private readonly CustomWebApplicationFactory<NotificationsController> factory;
    public NotificationControllerTests(CustomWebApplicationFactory<NotificationsController> factory) => this.factory = factory;

    private HttpClient Client(Guid userId, string role = "CUSTOMER", bool missingClaim = false)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        if (!missingClaim) claims.Add(new(ClaimTypes.NameIdentifier, userId.ToString()));
        var jwt = new JwtSecurityToken("FleetFlow.IdentityService", "FleetFlow.Client", claims,
            expires: DateTime.UtcNow.AddHours(1), signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("TestSigningKeyAtLeast32BytesLongSoItIsValidAndDoesNotError")), SecurityAlgorithms.HmacSha256));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
        return client;
    }

    [Theory]
    [InlineData("api/notifications")]
    [InlineData("api/notifications/unread-count")]
    public async Task AnonymousCannotReadInbox(string endpoint)
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(endpoint)).StatusCode);
    }

    [Fact]
    public async Task AnonymousCannotMarkRead()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PatchAsync($"api/notifications/{Guid.NewGuid()}/read", null)).StatusCode);
    }

    [Fact]
    public async Task SignedTokenWithoutUserIdCannotRead()
    {
        using var client = Client(Guid.Empty, missingClaim: true);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("api/notifications")).StatusCode);
    }

    [Fact]
    public async Task NewUserReceivesEmptyInboxAndZeroCount()
    {
        using var client = Client(Guid.NewGuid());
        Assert.Empty((await client.GetFromJsonAsync<NotificationResponse[]>("api/notifications"))!);
        Assert.Equal(0, (await client.GetFromJsonAsync<UnreadCount>("api/notifications/unread-count"))!.Count);
    }

    private record UnreadCount(int Count);

    [Fact]
    public async Task SystemNotification_AdminCreates_TargetReads_OtherUsersCannotAccessOrUpdate()
    {
        var target = Guid.NewGuid();
        using var admin = Client(Guid.NewGuid(), "ADMIN");
        var request = new CreateSystemNotificationRequest { TargetUserId = target, Title = "System update", Message = "Account message" };
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("api/notifications/system", request)).StatusCode);
        using var owner = Client(target);
        var notification = Assert.Single((await owner.GetFromJsonAsync<NotificationResponse[]>("api/notifications"))!);
        Assert.False(notification.IsRead);
        Assert.Equal(1, (await owner.GetFromJsonAsync<UnreadCount>("api/notifications/unread-count"))!.Count);
        // Administrator read/update privileges are still limited to their own inbox.
        Assert.Empty((await admin.GetFromJsonAsync<NotificationResponse[]>($"api/notifications?userId={target}"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PatchAsync($"api/notifications/{notification.Id}/read", null)).StatusCode);
        using var other = Client(Guid.NewGuid());
        Assert.Empty((await other.GetFromJsonAsync<NotificationResponse[]>($"api/notifications?targetUserId={target}"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PatchAsync($"api/notifications/{notification.Id}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PatchAsync($"api/notifications/{notification.Id}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PatchAsync($"api/notifications/{notification.Id}/read", null)).StatusCode);
        Assert.True(Assert.Single((await owner.GetFromJsonAsync<NotificationResponse[]>("api/notifications"))!).IsRead);
        Assert.Equal(0, (await owner.GetFromJsonAsync<UnreadCount>("api/notifications/unread-count"))!.Count);
    }

    [Fact]
    public async Task CustomerCannotCreateSystemNotifications()
    {
        using var client = Client(Guid.NewGuid());
        var response = await client.PostAsJsonAsync("api/notifications/system", new CreateSystemNotificationRequest
        { TargetUserId = Guid.NewGuid(), Title = "Hello", Message = "Private message" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InvalidSystemNotificationIsRejected()
    {
        using var client = Client(Guid.NewGuid(), "ADMIN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("api/notifications/system", new CreateSystemNotificationRequest
        { TargetUserId = Guid.Empty, Title = "Hello", Message = "Private message" })).StatusCode);
    }

    [Fact]
    public async Task BookingCreation_BackgroundDispatchGeneratesCustomerNotificationWithKafkaDisabled()
    {
        var customer = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
            var category = new VehicleCategory { Id = Guid.NewGuid(), Name = "Notification test" };
            db.VehicleCategories.Add(category);
            db.Vehicles.Add(new Vehicle { Id = vehicleId, VehicleCategoryId = category.Id, Make = "Test", Model = "Car",
                Vin = vehicleId.ToString("N")[..17], LicensePlate = vehicleId.ToString("N")[..10], DailyRate = 100 });
            await db.SaveChangesAsync();
        }
        using var client = Client(customer);
        var response = await client.PostAsJsonAsync("api/bookings", new CreateBookingRequest
        { VehicleId = vehicleId, StartDateTime = DateTime.UtcNow.AddDays(1), EndDateTime = DateTime.UtcNow.AddDays(2) });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booking = (await response.Content.ReadFromJsonAsync<BookingResponse>())!;
        NotificationResponse[] inbox = [];
        for (var attempt = 0; attempt < 50; attempt++)
        {
            inbox = (await client.GetFromJsonAsync<NotificationResponse[]>("api/notifications"))!;
            if (inbox.Length > 0) break;
            await Task.Delay(50);
        }
        Assert.Equal(booking.Id, Assert.Single(inbox).RelatedEntityId);
        Assert.Equal(vehicleId, inbox[0].VehicleId);
        using var freshScope = factory.Services.CreateScope();
        Assert.True(await freshScope.ServiceProvider.GetRequiredService<FleetDbContext>().Bookings.AnyAsync(b => b.Id == booking.Id));
    }
}
