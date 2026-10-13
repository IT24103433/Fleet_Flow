using FleetFlow.Contracts;
using FleetService.Api.Messaging.Events;
using FleetService.Api.Services;
namespace FleetService.Api.Messaging;
public class NotificationEventProcessor(NotificationService notifications)
{
    public static readonly System.Text.Json.JsonSerializerOptions JsonOptions = NotificationEventRendering.JsonOptions;
    public static NotificationDomainEvent Deserialize(string payload) => NotificationEventRendering.Deserialize(payload);
    public async Task ProcessAsync(NotificationDomainEvent domainEvent, CancellationToken ct = default)
    {
        foreach (var item in NotificationEventRendering.Render(domainEvent))
            await notifications.PersistAsync(item.EventId, item.TargetUserId, item.Category, item.Title,
                item.Message, item.RelatedEntityId, item.VehicleId, ct);
    }
}
