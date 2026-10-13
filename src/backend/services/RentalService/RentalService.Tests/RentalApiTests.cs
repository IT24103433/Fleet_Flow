using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FleetFlow.Contracts;
using FleetFlow.ServiceDefaults;
using FleetService.Api.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using RentalService.Api;
using RentalService.Api.Data;
using RentalService.Api.Services;
namespace RentalService.Tests;
public sealed class RentalApiTests
{
 [Fact]public async Task BookingCancellationAndAccountIsolationPreserveWireContracts()
 {
  using var factory=new RentalFactory();var customer=Guid.NewGuid();using var client=factory.Browser(customer);
  var request=new CreateBookingRequest{VehicleId=Guid.NewGuid(),StartDateTime=DateTime.UtcNow.AddDays(1),EndDateTime=DateTime.UtcNow.AddDays(3)};
  client.DefaultRequestHeaders.Add("Idempotency-Key",Guid.NewGuid().ToString());var response=await client.PostAsJsonAsync("/api/bookings",request);Assert.Equal(HttpStatusCode.Created,response.StatusCode);
  var row=await response.Content.ReadFromJsonAsync<BookingResponse>();Assert.NotNull(row);Assert.Equal(18000,row.TotalCost);Assert.Equal("Confirmed",JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("status").GetString());
  var repeat=await client.PostAsJsonAsync("/api/bookings",request);Assert.Equal(row.Id,(await repeat.Content.ReadFromJsonAsync<BookingResponse>())!.Id);
  using var other=factory.Browser(Guid.NewGuid());Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync($"/api/bookings/{row.Id}")).StatusCode);Assert.Equal(HttpStatusCode.NotFound,(await other.PostAsync($"/api/bookings/{row.Id}/cancel",null)).StatusCode);
  using var admin=factory.Browser(Guid.NewGuid(),"ADMIN");Assert.Equal(HttpStatusCode.OK,(await admin.GetAsync($"/api/bookings/{row.Id}")).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await admin.PostAsJsonAsync("/api/bookings",request)).StatusCode);
  Assert.Equal(HttpStatusCode.OK,(await client.PostAsync($"/api/bookings/{row.Id}/cancel",null)).StatusCode);Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsync($"/api/bookings/{row.Id}/cancel",null)).StatusCode);
  var mine=Assert.Single((await client.GetFromJsonAsync<BookingResponse[]>("/api/bookings"))!);Assert.Equal(FleetService.Api.Entities.BookingStatus.Cancelled,mine.Status);
 }
 [Fact]public async Task ForcedPasswordInactiveAndInvalidRequestsCannotWriteBookings()
 {
  using var active=new RentalFactory();using var forced=active.Browser(Guid.NewGuid(),"CUSTOMER",true);Assert.Equal(HttpStatusCode.Forbidden,(await forced.GetAsync("/api/bookings")).StatusCode);
  using var inactive=new RentalFactory(false);using var client=inactive.Browser(Guid.NewGuid());Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.GetAsync("/api/bookings")).StatusCode);
  using var normal=active.Browser(Guid.NewGuid());normal.DefaultRequestHeaders.Add("Idempotency-Key","invalid");Assert.Equal(HttpStatusCode.BadRequest,(await normal.PostAsJsonAsync("/api/bookings",new CreateBookingRequest())).StatusCode);
  normal.DefaultRequestHeaders.Remove("Idempotency-Key");Assert.Equal(HttpStatusCode.BadRequest,(await normal.PostAsJsonAsync("/api/bookings",new CreateBookingRequest{VehicleId=Guid.NewGuid(),StartDateTime=DateTime.UtcNow.AddDays(-1),EndDateTime=DateTime.UtcNow})).StatusCode);
 }
 [Fact]public async Task ReportsRequireInternalAudienceAndRestrictedServiceScope()
 {
  using var f=new RentalFactory();using var browser=f.Browser(Guid.NewGuid(),"ADMIN");Assert.Equal(HttpStatusCode.Unauthorized,(await browser.GetAsync("/internal/reports/statistics")).StatusCode);
  using var rental=f.Internal("rental-service");Assert.Equal(HttpStatusCode.Forbidden,(await rental.GetAsync("/internal/reports/statistics")).StatusCode);
  using var fleet=f.Internal("fleet-service");Assert.Equal(HttpStatusCode.OK,(await fleet.GetAsync("/internal/reports/statistics")).StatusCode);Assert.Equal(HttpStatusCode.OK,(await fleet.GetAsync("/internal/reports/bookings")).StatusCode);Assert.Equal(HttpStatusCode.Unauthorized,(await fleet.GetAsync("/api/bookings")).StatusCode);
 }
 [Fact]public async Task ForeignBookingsRemainNotFoundDuringFleetOutages()
 {
  using var f=new RentalFactory();var owner=Guid.NewGuid();using var client=f.Browser(owner);
  var response=await client.PostAsJsonAsync("/api/bookings",new CreateBookingRequest{VehicleId=Guid.NewGuid(),StartDateTime=DateTime.UtcNow.AddDays(1),EndDateTime=DateTime.UtcNow.AddDays(2)});
  var row=(await response.Content.ReadFromJsonAsync<BookingResponse>())!;f.Gateway.VehicleUnavailable=true;
  using var foreign=f.Browser(Guid.NewGuid());Assert.Equal(HttpStatusCode.NotFound,(await foreign.GetAsync($"/api/bookings/{row.Id}")).StatusCode);
  Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.GetAsync($"/api/bookings/{row.Id}")).StatusCode);
 }
 [Fact]public async Task AnonymousAvailabilityRetainsValidationAndConflictShape()
 {
  using var f=new RentalFactory();using var client=f.CreateClient();Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/api/bookings/check-availability")).StatusCode);
  var url=$"/api/bookings/check-availability?vehicleId={Guid.NewGuid()}&startDateTime={Uri.EscapeDataString(DateTime.UtcNow.AddDays(1).ToString("O"))}&endDateTime={Uri.EscapeDataString(DateTime.UtcNow.AddDays(2).ToString("O"))}";
  Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(url)).StatusCode);f.Gateway.Available=false;var response=await client.GetAsync(url);Assert.Equal(HttpStatusCode.Conflict,response.StatusCode);Assert.False(JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("isAvailable").GetBoolean());
 }
}
public sealed class RentalFactory(bool active=true):WebApplicationFactory<RentalApplication>
{
 public const string UserKey="rental-tests-user-key-123456789012345678901234";public const string InternalKey="rental-tests-internal-key-12345678901234567890";public const string Secret="rental-test-private-client-secret-1234567890";
 private readonly string database=Guid.NewGuid().ToString();public MemoryGateway Gateway{get;}=new();
 protected override void ConfigureWebHost(IWebHostBuilder builder)
 {
  builder.UseEnvironment("Development");builder.UseSetting("Jwt:Key",UserKey);builder.UseSetting("SERVICE_AUTH_ENABLED","true");builder.UseSetting("SERVICE_AUTH_SIGNING_KEY",InternalKey);
  foreach(var id in ServiceScopes.ClientGrants.Keys)builder.UseSetting($"ServiceAuth:Clients:{id}",Secret);
  builder.UseSetting("RENTAL_SERVICE_ACTIVE",active.ToString());builder.UseSetting("RENTAL_MIGRATION_ID",Guid.NewGuid().ToString());
  builder.ConfigureServices(services=>{
   foreach(var d in services.Where(d=>d.ServiceType==typeof(RentalDbContext)||d.ServiceType.FullName?.Contains("EntityFrameworkCore")==true||d.ServiceType.FullName?.Contains("Npgsql")==true).ToList())services.Remove(d);
   services.AddDbContext<RentalDbContext>(o=>o.UseInMemoryDatabase(database));services.RemoveAll<IDatabaseReadiness>();services.AddSingleton<IDatabaseReadiness>(new Ready());services.RemoveAll<IFleetGateway>();services.AddSingleton<IFleetGateway>(Gateway);services.RemoveAll<IHostedService>();
  });
 }
 public HttpClient Browser(Guid user,string role="CUSTOMER",bool forced=false)
 {
  var token=new JwtSecurityToken("FleetFlow.IdentityService","FleetFlow.Client",[new("sub",user.ToString()),new(ClaimTypes.Role,role),new("must_change_password",forced.ToString().ToLowerInvariant())],DateTime.UtcNow.AddSeconds(-1),DateTime.UtcNow.AddMinutes(5),new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(UserKey)),SecurityAlgorithms.HmacSha256));
  var client=CreateClient();client.DefaultRequestHeaders.Authorization=new("Bearer",new JwtSecurityTokenHandler().WriteToken(token));return client;
 }
 public HttpClient Internal(string id)
 {var options=new ServiceAuthenticationOptions(true,InternalKey,"FleetFlow.IdentityService",ServiceScopes.ClientGrants.Keys.ToDictionary(k=>k,_=>Secret));var client=CreateClient();client.DefaultRequestHeaders.Authorization=new("Bearer",new ServiceTokenIssuer(options,TimeProvider.System).Issue(new(id,Secret))!.AccessToken);return client;}
 private sealed class Ready:IDatabaseReadiness{public Task<bool> IsReadyAsync(CancellationToken ct)=>Task.FromResult(true);}
 public sealed class MemoryGateway:IFleetGateway
 {
  public bool Available{get;set;}=true;public bool VehicleUnavailable{get;set;}private readonly Dictionary<Guid,ReservationAcknowledgement> rows=[];
  public Task<ReservationAcknowledgement> ReserveAsync(ReserveVehicleCommand c,CancellationToken ct){if(!rows.TryGetValue(c.BookingId,out var row))rows[c.BookingId]=row=new(c.BookingId,false,18000,new(){Id=c.VehicleId,Make="Toyota",Model="Corolla",DailyRate=9000});return Task.FromResult(row);}
  public Task<ReservationAcknowledgement> ReleaseAsync(Guid id,CancellationToken ct){var row=rows[id] with{Released=true};rows[id]=row;return Task.FromResult(row);}
  public Task<VehicleResponse> VehicleAsync(Guid id,CancellationToken ct)=>VehicleUnavailable?throw new BookingRequestException(503,"Fleet unavailable."):Task.FromResult(new VehicleResponse{Id=id,Make="Toyota",Model="Corolla",DailyRate=9000});
  public Task<bool> AvailableAsync(Guid vehicleId,DateTime start,DateTime end,CancellationToken ct)=>Task.FromResult(Available);
 }
}
