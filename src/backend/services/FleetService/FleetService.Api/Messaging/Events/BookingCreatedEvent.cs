namespace FleetService.Api.Messaging.Events;

public record BookingCreatedEvent : NotificationDomainEvent
{
    public override string EventType => nameof(BookingCreatedEvent);
    public Guid BookingId { get; init; }
    public Guid CustomerId { get; init; }
    public Guid VehicleId { get; init; }
}
