using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FleetService.Api.Dtos;
namespace FleetFlow.Contracts;
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RentalLifecycleState { PickupPending, Active, CompletionPending, Completed }
public sealed class PickupRequest
{
 [JsonRequired,Range(0,int.MaxValue)]public int Odometer {get;set;}
 [JsonRequired,Range(0,100)]public int FuelPercentage {get;set;}
 [Required,StringLength(2000,MinimumLength=3)]public string Condition {get;set;}="";
}
public sealed record RentalHoldCommand(Guid BookingId,Guid RentalId,int Odometer,DateTime PickupAt);
public sealed record RentalHoldAcknowledgement(Guid BookingId,Guid RentalId,bool Held);
public sealed record RentalResponse(Guid Id,Guid BookingId,RentalLifecycleState State,DateTime PickupAt,int PickupOdometer,int PickupFuelPercentage,string PickupCondition,Guid PickedUpByUserId,BookingResponse Booking) { [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] public RentalReturnResponse? Return {get;init;} };
public sealed record RentalQueueItem(BookingResponse Booking,RentalResponse? Rental);
public sealed record RentalQueueResponse(IReadOnlyList<RentalQueueItem> Items,int TotalCount,int Page,int PageSize,int TotalPages);
