using System.Text.Json;
using FleetService.Api.Messaging.Events;
using FleetService.Api.Services;

namespace FleetService.Api.Messaging;

public class NotificationEventProcessor(NotificationService notifications)
{
    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

    public static NotificationDomainEvent Deserialize(string payload)
    {
        using var json = JsonDocument.Parse(payload);
        if (json.RootElement.ValueKind != JsonValueKind.Object ||
            !json.RootElement.TryGetProperty("eventType", out var discriminator) || discriminator.ValueKind != JsonValueKind.String)
            throw new ArgumentException("An eventType string is required.");
        var type = discriminator.GetString();
        return type switch
        {
            nameof(BookingCreatedEvent) => JsonSerializer.Deserialize<BookingCreatedEvent>(payload, JsonOptions)!,
            nameof(BookingCancelledEvent) => JsonSerializer.Deserialize<BookingCancelledEvent>(payload, JsonOptions)!,
            nameof(MaintenanceScheduledEvent) => JsonSerializer.Deserialize<MaintenanceScheduledEvent>(payload, JsonOptions)!,
            nameof(MaintenanceStatusChangedEvent) => JsonSerializer.Deserialize<MaintenanceStatusChangedEvent>(payload, JsonOptions)!,
            _ => throw new ArgumentException($"Unsupported notification event type: {type}")
        };
    }

    public async Task ProcessAsync(NotificationDomainEvent domainEvent, CancellationToken ct = default)
    {
        if (domainEvent.EventId == Guid.Empty || domainEvent.OccurredAt == default || domainEvent.OccurredAt.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A stable event ID and UTC occurrence timestamp are required.");
        Guid relatedId;
        Guid vehicleId;
        Guid[] recipients;
        string category, title, message;
        switch (domainEvent)
        {
            case BookingCreatedEvent e:
                (relatedId, vehicleId, recipients) = (e.BookingId, e.VehicleId, [e.CustomerId]);
                (category, title, message) = ("Booking", "Booking created", $"Booking {e.BookingId} for vehicle {e.VehicleId} was created successfully.");
                break;
            case BookingCancelledEvent e:
                (relatedId, vehicleId, recipients) = (e.BookingId, e.VehicleId, [e.CustomerId]);
                (category, title, message) = ("Booking", "Booking cancelled", $"Booking {e.BookingId} for vehicle {e.VehicleId} was cancelled.");
                break;
            case MaintenanceScheduledEvent e:
                ValidateText(e.Activity, 200);
                (relatedId, vehicleId, recipients) = (e.MaintenanceId, e.VehicleId, e.TargetUserIds);
                (category, title, message) = ("Maintenance", "Maintenance scheduled", $"{e.Activity} ({e.MaintenanceId}) was scheduled for vehicle {e.VehicleId}.");
                break;
            case MaintenanceStatusChangedEvent e:
                ValidateText(e.Activity, 200);
                ValidateText(e.Status, 50);
                (relatedId, vehicleId, recipients) = (e.MaintenanceId, e.VehicleId, e.TargetUserIds);
                (category, title, message) = ("Maintenance", "Maintenance status updated", $"{e.Activity} ({e.MaintenanceId}) for vehicle {e.VehicleId} is now {e.Status}.");
                break;
            default: throw new ArgumentException("Unsupported notification event.");
        }
        // Validate the entire recipient list before persisting any recipient.
        if (relatedId == Guid.Empty || vehicleId == Guid.Empty || recipients == null ||
            recipients.Length == 0 || recipients.Length > 1000 || recipients.Any(id => id == Guid.Empty))
            throw new ArgumentException("Related entity, vehicle and authorized target users are required.");
        foreach (var userId in recipients.Distinct())
            await notifications.PersistAsync(domainEvent.EventId, userId, category, title, message, relatedId, vehicleId, ct);
    }

    private static void ValidateText(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > maxLength) throw new ArgumentException("Invalid maintenance activity or status.");
    }
}
