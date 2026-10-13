namespace FleetService.Api.Messaging.Events;

// EventId must be stable across retries. Producers allocate it once before committing the business operation and outbox.
public abstract record NotificationDomainEvent
{
    public Guid EventId { get; init; }
    public DateTime OccurredAt { get; init; }
    public abstract string EventType { get; }
}
