namespace FleetService.Api.Messaging.Events;

// EventId must be stable across retries. Producers set it once after a successful operation.
public abstract record NotificationDomainEvent
{
    public Guid EventId { get; init; }
    public DateTime OccurredAt { get; init; }
    public abstract string EventType { get; }
}
