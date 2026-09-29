using System.Threading.Channels;
using FleetService.Api.Messaging.Events;
using Microsoft.Extensions.Options;

namespace FleetService.Api.Messaging;

public interface INotificationEventDispatcher
{
    bool TryEnqueue(NotificationDomainEvent domainEvent);
}

// Bounded, best-effort dispatch after the business transaction commits. Never awaited by REST.
public class NotificationEventDispatcher(IServiceScopeFactory scopes, IKafkaProducerService producer,
    IOptions<KafkaSettings> settings, ILogger<NotificationEventDispatcher> logger) : BackgroundService, INotificationEventDispatcher
{
    private readonly Channel<NotificationDomainEvent> queue = Channel.CreateBounded<NotificationDomainEvent>(
        new BoundedChannelOptions(1000) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    public bool TryEnqueue(NotificationDomainEvent domainEvent)
    {
        var accepted = queue.Writer.TryWrite(domainEvent);
        if (!accepted) logger.LogWarning("Notification queue full; event {EventId} could not be queued.", domainEvent.EventId);
        return accepted;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var domainEvent in queue.Reader.ReadAllAsync(stoppingToken))
                await DispatchAsync(domainEvent, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public async Task DispatchAsync(NotificationDomainEvent domainEvent, CancellationToken ct = default)
    {
        // Each attempt owns a fresh context: no notification changes share the booking transaction.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<NotificationEventProcessor>().ProcessAsync(domainEvent, ct);
                break;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Notification persistence failed for event {EventId}, attempt {Attempt}.", domainEvent.EventId, attempt + 1);
                if (attempt < 2) await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
        try
        {
            var topic = domainEvent is BookingCreatedEvent or BookingCancelledEvent
                ? settings.Value.BookingEventsTopic : settings.Value.MaintenanceEventsTopic;
            // Serialize using the runtime contract (the producer serializes objects with runtime properties).
            await producer.PublishAsync<object>(topic, domainEvent.EventId.ToString(), domainEvent, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogWarning(ex, "Kafka dispatch failed for notification event {EventId}.", domainEvent.EventId); }
    }
}
