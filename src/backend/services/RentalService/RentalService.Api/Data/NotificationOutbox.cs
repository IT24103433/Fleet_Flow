using System.Text.Json;
using FleetFlow.Contracts;
using FleetService.Api.Messaging.Events;
namespace RentalService.Api.Data;
public sealed class NotificationOutbox
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = "";
    public string Payload { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime? LockedUntil { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTime? InboxDeliveredAt { get; set; }
    public DateTime? KafkaPublishedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? LastErrorCode { get; set; }
    public int ReplayCount { get; set; }
    public Guid? LastReplayedByUserId { get; set; }
    public DateTime? LastReplayedAt { get; set; }
    public static NotificationOutbox From(NotificationDomainEvent item)
    {
        NotificationEventRendering.Render(item); // Reject invalid events before the business SaveChanges.
        return new() { Id=item.EventId, EventType=item.EventType, CreatedAt=item.OccurredAt,
            NextAttemptAt=item.OccurredAt, Payload=JsonSerializer.Serialize(item,item.GetType(),NotificationEventRendering.JsonOptions) };
    }
}
