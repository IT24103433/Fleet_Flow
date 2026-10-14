namespace FleetService.Api.Entities;
// Fleet-owned availability block. Released rows remain as idempotency tombstones.
public sealed class VehicleReservation
{
 public Guid BookingId {get;set;}
 public Guid VehicleId {get;set;}
 public Guid CustomerId {get;set;}
 public DateTime StartDateTime {get;set;}
 public DateTime EndDateTime {get;set;}
 public bool IsHeld {get;set;}
 public Guid? HeldRentalId {get;set;}
 public int? PickupOdometer {get;set;}
 public DateTime? PickupAt {get;set;}
 public DateTime? ReturnedAt {get;set;}
 public int? ReturnOdometer {get;set;}
 public bool Released {get;set;}
 public decimal TotalCost {get;set;}
 public string VehicleSnapshot {get;set;}="";
 public DateTime CreatedAt {get;set;}
 public DateTime? ReleasedAt {get;set;}
}
