using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FleetFlow.Contracts;
using FleetService.Api.Dtos;
using FleetFlow.ServiceDefaults;
namespace RentalService.Api.Services;
public sealed class BookingRequestException(int status,string message):Exception(message){public int Status {get;}=status;}
public interface IFleetGateway
{
 Task<RentalCompletionAcknowledgement> CompleteAsync(RentalCompletionCommand command,CancellationToken ct);
 Task<RentalHoldAcknowledgement> HoldAsync(RentalHoldCommand command,CancellationToken ct);
 Task<ReservationAcknowledgement> ReserveAsync(ReserveVehicleCommand command,CancellationToken ct);
 Task<ReservationAcknowledgement> ReleaseAsync(Guid bookingId,CancellationToken ct);
 Task<VehicleResponse> VehicleAsync(Guid vehicleId,CancellationToken ct);
 Task<bool> AvailableAsync(Guid vehicleId,DateTime start,DateTime end,CancellationToken ct);
}
public sealed class FleetGateway(IHttpClientFactory clients,IServiceTokenProvider tokens):IFleetGateway
{
 private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){Converters={new JsonStringEnumConverter()}};
 private async Task<T> SendAsync<T>(HttpMethod method,string path,object? body,CancellationToken ct)
 {
  try{
   using var request=new HttpRequestMessage(method,path);
   request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",await tokens.GetTokenAsync(ct));
   if(body!=null)request.Content=JsonContent.Create(body,options:Json);
   using var response=await clients.CreateClient("FleetService").SendAsync(request,ct);
   if(!response.IsSuccessStatusCode)
   {
    var status=(int)response.StatusCode;
    var error="Vehicle availability could not be confirmed.";
    if(status is 400 or 404 or 409){var payload=await response.Content.ReadFromJsonAsync<JsonElement>(ct);if(payload.ValueKind==JsonValueKind.Object&&payload.TryGetProperty("message",out var message))error=message.GetString()??error;}
    throw new BookingRequestException(status is 400 or 404 or 409?status:503,error);
   }
   return await response.Content.ReadFromJsonAsync<T>(Json,ct)??throw new BookingRequestException(503,"Fleet returned an incomplete acknowledgement.");
  }
  catch(BookingRequestException){throw;}
  catch(Exception ex)when(ex is HttpRequestException or JsonException or InvalidOperationException || ex is TaskCanceledException&&!ct.IsCancellationRequested)
  {throw new BookingRequestException(503,"Vehicle availability is temporarily unavailable. Check My bookings before retrying.");}
 }
 public Task<VehicleResponse> VehicleAsync(Guid vehicleId,CancellationToken ct)=>SendAsync<VehicleResponse>(HttpMethod.Get,$"internal/vehicles/{vehicleId}",null,ct);
 public Task<RentalCompletionAcknowledgement> CompleteAsync(RentalCompletionCommand command,CancellationToken ct)=>SendAsync<RentalCompletionAcknowledgement>(HttpMethod.Post,$"internal/reservations/{command.BookingId}/complete",command,ct);
 public Task<RentalHoldAcknowledgement> HoldAsync(RentalHoldCommand command,CancellationToken ct)=>SendAsync<RentalHoldAcknowledgement>(HttpMethod.Post,$"internal/reservations/{command.BookingId}/pickup",command,ct);
 public Task<ReservationAcknowledgement> ReserveAsync(ReserveVehicleCommand command,CancellationToken ct)=>SendAsync<ReservationAcknowledgement>(HttpMethod.Put,$"internal/reservations/{command.BookingId}",command,ct);
 public Task<ReservationAcknowledgement> ReleaseAsync(Guid id,CancellationToken ct)=>SendAsync<ReservationAcknowledgement>(HttpMethod.Post,$"internal/reservations/{id}/release",null,ct);
 public async Task<bool> AvailableAsync(Guid vehicleId,DateTime start,DateTime end,CancellationToken ct)
 {var value=await SendAsync<JsonElement>(HttpMethod.Get,$"internal/reservations/availability?vehicleId={vehicleId}&startDateTime={Uri.EscapeDataString(start.ToString("O"))}&endDateTime={Uri.EscapeDataString(end.ToString("O"))}",null,ct);return value.GetProperty("isAvailable").GetBoolean();}
}
