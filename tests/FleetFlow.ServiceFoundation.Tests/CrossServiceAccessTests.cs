using System.Net;
using System.Net.Http.Json;
using FleetFlow.ServiceDefaults;
using FleetService.Api.Controllers;
using FleetService.Api.Data;
using IdentityService.Api;
using IdentityService.Api.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NotificationService.Api;
using RentalService.Api;
namespace FleetFlow.ServiceFoundation.Tests;

public sealed class CrossServiceAccessTests
{
    [FoundationPostgresFact]
    public async Task CriticalChangesRevokeAcrossFourRealHttpPipelinesAndIdentityOutageFailsClosed()
    {
        var settings = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FLEET_TEST_POSTGRES"));
        if (settings.Host is not ("localhost" or "127.0.0.1") || settings.Database?.StartsWith("fleetflow_hardening_", StringComparison.Ordinal) != true) throw new InvalidOperationException("Unsafe test target.");
        settings.Database = "postgres"; await using var postgres = new Npgsql.NpgsqlConnection(settings.ConnectionString); await postgres.OpenAsync();
        var database = "fleetflow_hardening_crossaccess_" + Guid.NewGuid().ToString("N")[..12];
        await using (var create = new Npgsql.NpgsqlCommand($"CREATE DATABASE {database}", postgres)) await create.ExecuteNonQueryAsync();
        settings.Database = database;
        try {
            using var identityHost = new AccessHost<IdentityApplication>(settings.ConnectionString);
            using var identity = identityHost.CreateClient();
            var ready = false;
            for (var i = 0; i < 100; i++) { if ((await identity.GetAsync("/health/ready")).StatusCode == HttpStatusCode.OK) { ready = true; break; } await Task.Delay(100); }
            Assert.True(ready, "Isolated Identity schema and seeding must become ready.");
            var bridge = new IdentityBridge(identity);
            using var fleetHost = new AccessHost<VehiclesController>(bridge: bridge);
            using var rentalHost = new AccessHost<RentalApplication>(bridge: bridge);
            using var notificationHost = new AccessHost<NotificationApplication>(bridge: bridge);
            using var fleet = fleetHost.CreateClient(); using var rental = rentalHost.CreateClient(); using var notification = notificationHost.CreateClient();
            var protectedRoutes = new[] { (identity, "/api/auth/profile-picture"), (fleet, "/api/notifications"), (rental, "/api/service"), (notification, "/api/service") };
            async Task Check(string token, HttpStatusCode expected) {
                foreach (var (client, path) in protectedRoutes) { using var request = new HttpRequestMessage(HttpMethod.Get, path); request.Headers.Authorization = new("Bearer", token); Assert.Equal(expected, (await client.SendAsync(request)).StatusCode); }
            }
            async Task<LoginResponse> Login(string username, string password, string channel) {
                var response = await identity.PostAsJsonAsync("/api/auth/login", new LoginRequest { UsernameOrEmail = username, Password = password, LoginChannel = channel });
                Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
            }
            async Task<HttpResponseMessage> Write(HttpMethod method, string path, string token, object? body = null) {
                using var request = new HttpRequestMessage(method, path); request.Headers.Authorization = new("Bearer", token); if (body != null) request.Content = JsonContent.Create(body); return await identity.SendAsync(request);
            }
            var admin = await Login("admin", "Password@123", "staff");
            var customer = await Login("customer", "Password@123", "customer");
            await Check(customer.Token, HttpStatusCode.OK);
            Assert.Equal(HttpStatusCode.OK, (await Write(HttpMethod.Post, "/api/auth/change-password", customer.Token, new { currentPassword = "Password@123", newPassword = "ChangedPassword@123" })).StatusCode);
            await Check(customer.Token, HttpStatusCode.Unauthorized);
            customer = await Login("customer", "ChangedPassword@123", "customer"); await Check(customer.Token, HttpStatusCode.OK);
            Assert.Equal(HttpStatusCode.OK, (await Write(HttpMethod.Post, $"/api/admin/users/{customer.User.Id}/reset-password", admin.Token, new { customTemporaryPassword = "TemporaryPassword@123", forcePasswordChange = true })).StatusCode);
            await Check(customer.Token, HttpStatusCode.Unauthorized);
            var forced = await Login("customer", "TemporaryPassword@123", "customer"); await Check(forced.Token, HttpStatusCode.Forbidden);
            var changed = await Write(HttpMethod.Post, "/api/auth/change-password", forced.Token, new { newPassword = "PermanentPassword@123" });
            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            var renewed = (await changed.Content.ReadFromJsonAsync<LoginResponse>())!; Assert.False(renewed.User.MustChangePassword);
            await Check(forced.Token, HttpStatusCode.Unauthorized); await Check(renewed.Token, HttpStatusCode.OK);
            Assert.Equal(HttpStatusCode.OK, (await Write(HttpMethod.Put, $"/api/admin/users/{customer.User.Id}", admin.Token, new { username = "customer", email = "customer@fleetflow.io", fullName = "Actual customer", role = "FLEET_MANAGER" })).StatusCode);
            await Check(renewed.Token, HttpStatusCode.Unauthorized);
            var staff = await Login("customer", "PermanentPassword@123", "staff"); await Check(staff.Token, HttpStatusCode.OK);
            Assert.Equal(HttpStatusCode.OK, (await Write(HttpMethod.Patch, $"/api/admin/users/{customer.User.Id}/status", admin.Token, new { isActive = false })).StatusCode);
            await Check(staff.Token, HttpStatusCode.Unauthorized);
            Assert.Equal(HttpStatusCode.OK, (await Write(HttpMethod.Patch, $"/api/admin/users/{customer.User.Id}/status", admin.Token, new { isActive = true })).StatusCode);
            await Check(staff.Token, HttpStatusCode.Unauthorized);
            staff = await Login("customer", "PermanentPassword@123", "staff"); await Check(staff.Token, HttpStatusCode.OK);
            // The restricted internal audience remains separate from browser authentication.
            using (var request = new HttpRequestMessage(HttpMethod.Get, $"/internal/accounts/{customer.User.Id}/access")) { request.Headers.Authorization = new("Bearer", staff.Token); Assert.Equal(HttpStatusCode.Unauthorized, (await identity.SendAsync(request)).StatusCode); }
            bridge.Offline = true;
            foreach (var (client, path) in protectedRoutes.Skip(1)) { using var request = new HttpRequestMessage(HttpMethod.Get, path); request.Headers.Authorization = new("Bearer", staff.Token); Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.SendAsync(request)).StatusCode); }
            Assert.Equal(HttpStatusCode.OK, (await fleet.GetAsync("/api/vehicles")).StatusCode);
            bridge.Offline = false;
            Assert.Equal(HttpStatusCode.NoContent, (await Write(HttpMethod.Delete, $"/api/admin/users/{customer.User.Id}", admin.Token)).StatusCode);
            await Check(staff.Token, HttpStatusCode.Unauthorized);
        } finally {
            if (!System.Text.RegularExpressions.Regex.IsMatch(database, "^fleetflow_hardening_crossaccess_[a-f0-9]{12}$")) throw new InvalidOperationException("Unsafe cleanup.");
            using (var pool = new Npgsql.NpgsqlConnection(settings.ConnectionString)) Npgsql.NpgsqlConnection.ClearPool(pool);
            await using var drop = new Npgsql.NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", postgres); await drop.ExecuteNonQueryAsync();
        }
    }
    private sealed class IdentityBridge(HttpClient identity) : HttpMessageHandler
    {
        public bool Offline;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            if (Offline) throw new HttpRequestException("Identity unavailable");
            using var forwarded = new HttpRequestMessage(request.Method, request.RequestUri!.PathAndQuery);
            foreach (var header in request.Headers) forwarded.Headers.TryAddWithoutValidation(header.Key, header.Value);
            if (request.Content != null) forwarded.Content = new StringContent(await request.Content.ReadAsStringAsync(ct), System.Text.Encoding.UTF8, "application/json");
            return await identity.SendAsync(forwarded, ct);
        }
    }
    private sealed class AccessHost<T>(string? identityConnection = null, IdentityBridge? bridge = null) : WebApplicationFactory<T> where T : class
    {
        private readonly string fleetDatabase = Guid.NewGuid().ToString();
        protected override void ConfigureWebHost(IWebHostBuilder builder) {
            builder.UseEnvironment("Development"); builder.UseSetting("Jwt:Key", SecurityTests.UserKey); builder.UseSetting("Jwt:Issuer", "FleetFlow.IdentityService"); builder.UseSetting("Jwt:Audience", "FleetFlow.Client");
            builder.UseSetting("SERVICE_AUTH_ENABLED", "true"); builder.UseSetting("SERVICE_AUTH_SIGNING_KEY", SecurityTests.InternalKey); builder.UseSetting("Kafka:Enabled", "false");
            builder.UseSetting("RENTAL_SERVICE_ACTIVE", "false"); builder.UseSetting("NOTIFICATION_SERVICE_ACTIVE", "false"); builder.UseSetting("NOTIFICATION_OWNERSHIP", "Legacy"); builder.UseSetting("RENTAL_OWNERSHIP", "Legacy");
            foreach (var id in FleetFlow.Contracts.ServiceScopes.ClientGrants.Keys) builder.UseSetting($"ServiceAuth:Clients:{id}", SecurityTests.Secret);
            if (identityConnection != null) builder.UseSetting("ConnectionStrings:DefaultConnection", identityConnection);
            builder.ConfigureServices(services => {
                services.RemoveAll<IHostedService>();
                if (typeof(T) == typeof(VehiclesController)) {
                    foreach (var d in services.Where(d => d.ServiceType == typeof(FleetDbContext) || d.ServiceType.FullName?.Contains("EntityFrameworkCore") == true || d.ServiceType.FullName?.Contains("Npgsql") == true).ToList()) services.Remove(d);
                    services.AddDbContext<FleetDbContext>(o => o.UseInMemoryDatabase(fleetDatabase));
                }
                if (bridge != null) {
                    var http = new HttpClient(bridge, disposeHandler: false) { BaseAddress = new Uri("http://localhost/"), Timeout = TimeSpan.FromSeconds(5) };
                    var service = typeof(T) == typeof(VehiclesController) ? "fleet-service" : typeof(T) == typeof(RentalApplication) ? "rental-service" : "notification-service";
                    services.RemoveAll<IServiceTokenProvider>(); services.AddSingleton<IServiceTokenProvider>(new ServiceTokenProvider(http, SecurityTests.Options(), service, TimeProvider.System));
                    services.RemoveAll<IAccountAccessReader>(); services.AddScoped<IAccountAccessReader>(sp => new RemoteAccountAccessReader(http, sp.GetRequiredService<IServiceTokenProvider>()));
                }
            });
        }
    }
}
