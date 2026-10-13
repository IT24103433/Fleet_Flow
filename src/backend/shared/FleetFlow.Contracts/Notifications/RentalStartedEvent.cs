namespace FleetService.Api.Messaging.Events;
public sealed record RentalStartedEvent:NotificationDomainEvent
{
 public override string EventType=>nameof(RentalStartedEvent);
 public Guid RentalId {get;init;}
 public Guid BookingId {get;init;}
 public Guid VehicleId {get;init;}
 public Guid CustomerId {get;init;}
}
