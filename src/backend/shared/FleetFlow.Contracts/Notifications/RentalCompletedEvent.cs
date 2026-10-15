namespace FleetService.Api.Messaging.Events;
public sealed record RentalCompletedEvent : NotificationDomainEvent
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public decimal? FinalRentalAmount {get;init;}
    public override string EventType => nameof(RentalCompletedEvent);
    public Guid RentalId { get; init; }
    public Guid BookingId { get; init; }
    public Guid VehicleId { get; init; }
    public Guid CustomerId { get; init; }
}
