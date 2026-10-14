namespace FleetService.Api.Messaging.Events;
public sealed record RentalCompletedEvent : NotificationDomainEvent
{
    public override string EventType => nameof(RentalCompletedEvent);
    public Guid RentalId { get; init; }
    public Guid BookingId { get; init; }
    public Guid VehicleId { get; init; }
    public Guid CustomerId { get; init; }
}
