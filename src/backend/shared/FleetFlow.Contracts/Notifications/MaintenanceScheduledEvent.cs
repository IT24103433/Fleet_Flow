namespace FleetService.Api.Messaging.Events;

public record MaintenanceScheduledEvent : NotificationDomainEvent
{
    public override string EventType => nameof(MaintenanceScheduledEvent);
    public Guid MaintenanceId { get; init; }
    public Guid VehicleId { get; init; }
    public string Activity { get; init; } = string.Empty;
    // The maintenance owner resolves assigned/authorized maintenance users, never the browser.
    public Guid[] TargetUserIds { get; init; } = [];
}
