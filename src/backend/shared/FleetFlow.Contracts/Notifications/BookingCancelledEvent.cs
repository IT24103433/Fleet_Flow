namespace FleetService.Api.Messaging.Events;

public record BookingCancelledEvent : NotificationDomainEvent
{
    public override string EventType => nameof(BookingCancelledEvent);
    public Guid BookingId { get; init; }
    public Guid CustomerId { get; init; }
    public Guid VehicleId { get; init; }
}
