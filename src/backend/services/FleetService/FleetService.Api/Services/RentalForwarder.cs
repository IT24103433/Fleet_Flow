using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FleetFlow.Contracts;
using FleetFlow.ServiceDefaults;
using FleetService.Api.Data;
using FleetService.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
namespace FleetService.Api.Services;
public sealed class RentalUnavailableException:Exception{public RentalUnavailableException():base("Rental service is unavailable. Please try again.") {}}
public sealed class RentalForwarder(RentalRouting routing,IHttpClientFactory clients,IServiceTokenProvider tokens,FleetDbContext db)
{
 public bool Enabled=>routing.Remote;
 private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){Converters={new JsonStringEnumConverter()}};
 public async Task<ActionResult> ForwardAsync(HttpRequest request,string path,object? body=null)
 {
  var ct=request.HttpContext.RequestAborted;
  if(!await routing.ReadyAsync(db,ct))return new ObjectResult(new{message="Rental ownership has not passed verification."}){StatusCode=503};
  using var message=new HttpRequestMessage(new HttpMethod(request.Method),"api/bookings"+path);
  message.Headers.TryAddWithoutValidation("Authorization",request.Headers.Authorization.ToString());
  if(request.Headers.TryGetValue("Idempotency-Key",out var key))message.Headers.TryAddWithoutValidation("Idempotency-Key",key.ToString());
  if(body!=null)message.Content=JsonContent.Create(body,options:Json);
  try{
   using var response=await clients.CreateClient("RentalService").SendAsync(message,ct);
   if(response.Headers.Location!=null)request.HttpContext.Response.Headers.Location="/api/bookings/"+response.Headers.Location.Segments.Last();
   return new ContentResult{StatusCode=(int)response.StatusCode,ContentType=response.Content.Headers.ContentType?.ToString()??"application/json",Content=await response.Content.ReadAsStringAsync(ct)};
  }catch(Exception ex)when(ex is HttpRequestException||ex is TaskCanceledException&&!ct.IsCancellationRequested){throw new RentalUnavailableException();}
 }
 private async Task<T> InternalAsync<T>(string path,CancellationToken ct)
 {
  if(!await routing.ReadyAsync(db,ct))throw new RentalUnavailableException();
  try{
   using var request=new HttpRequestMessage(HttpMethod.Get,path);request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",await tokens.GetTokenAsync(ct));
   using var response=await clients.CreateClient("RentalService").SendAsync(request,ct);response.EnsureSuccessStatusCode();
   return await response.Content.ReadFromJsonAsync<T>(Json,ct)??throw new RentalUnavailableException();
  }catch(Exception ex)when(ex is HttpRequestException or JsonException or InvalidOperationException||ex is TaskCanceledException&&!ct.IsCancellationRequested){throw new RentalUnavailableException();}
 }
 public Task<BookingReportResponse> ReportAsync(BookingReportQuery query,CancellationToken ct)=>InternalAsync<BookingReportResponse>($"internal/reports/bookings?page={query.Page}&pageSize={query.PageSize}"+(query.Status.HasValue?$"&status={query.Status}":""),ct);
 public Task<RentalStatisticsResponse> StatisticsAsync(CancellationToken ct)=>InternalAsync<RentalStatisticsResponse>("internal/reports/statistics",ct);
}
