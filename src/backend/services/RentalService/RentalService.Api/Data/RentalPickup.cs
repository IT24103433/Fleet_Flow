using FleetFlow.Contracts;
namespace RentalService.Api.Data;
public sealed class RentalPickup
{
 public Guid Id {get;set;}
 public Guid BookingId {get;set;}
 public RentalLifecycleState State {get;set;}=RentalLifecycleState.PickupPending;
 public DateTime PickupAt {get;set;}
 public int PickupOdometer {get;set;}
 public int PickupFuelPercentage {get;set;}
 public string PickupCondition {get;set;}="";
 public Guid PickedUpByUserId {get;set;}
}
