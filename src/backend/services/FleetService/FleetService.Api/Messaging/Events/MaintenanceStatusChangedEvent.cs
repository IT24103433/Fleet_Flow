namespace FleetService.Api.Messaging.Events;

public record MaintenanceStatusChangedEvent : NotificationDomainEvent
{
    public override string EventType => nameof(MaintenanceStatusChangedEvent);
    public Guid MaintenanceId { get; init; }
    public Guid VehicleId { get; init; }
    public string Activity { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid[] TargetUserIds { get; init; } = [];
}
