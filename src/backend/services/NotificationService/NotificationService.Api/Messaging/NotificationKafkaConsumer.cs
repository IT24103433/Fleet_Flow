using System.Text.Json;
using Confluent.Kafka;
using FleetService.Api.Messaging.Events;
using FleetFlow.Contracts;
using FleetFlow.ServiceDefaults;
using NotificationService.Api.Services;
using Microsoft.Extensions.Options;

namespace NotificationService.Api.Messaging;

public class NotificationKafkaConsumer(IServiceScopeFactory scopes, IOptions<KafkaSettings> options,
    ILogger<NotificationKafkaConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled) return;
        using (var scope = scopes.CreateScope())
            if (!scope.ServiceProvider.GetRequiredService<NotificationActivation>().Active) return;
        await Task.Yield();
        // Reconnect after initialization/consumer failures without stopping the API host.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var readinessScope=scopes.CreateScope())
                    if (!await readinessScope.ServiceProvider.GetRequiredService<IDatabaseReadiness>().IsReadyAsync(stoppingToken))
                    { await Task.Delay(2000,stoppingToken); continue; }
                using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
                {
                    BootstrapServers = settings.BootstrapServers, GroupId = settings.NotificationConsumerGroupId,
                    AutoOffsetReset = AutoOffsetReset.Earliest, EnableAutoCommit = false,
                    EnableAutoOffsetStore = false, AllowAutoCreateTopics = false, SocketTimeoutMs = 5000, SessionTimeoutMs = 10000
                }).Build();
                consumer.Subscribe(new[] { settings.BookingEventsTopic, settings.MaintenanceEventsTopic });
                try
                {
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        var result = consumer.Consume(TimeSpan.FromMilliseconds(500));
                        if (result == null) continue;
                        try
                        {
                            var domainEvent = NotificationEventRendering.Deserialize(result.Message.Value);
                            var expectedTopic = domainEvent is BookingCreatedEvent or BookingCancelledEvent
                                ? settings.BookingEventsTopic : settings.MaintenanceEventsTopic;
                            if (result.Topic != expectedTopic) throw new ArgumentException("Event received on the wrong domain topic.");
                            using var scope = scopes.CreateScope();
                            if (!await scope.ServiceProvider.GetRequiredService<IDatabaseReadiness>().IsReadyAsync(stoppingToken))
                                throw new InvalidOperationException("Notification migration activation is not ready.");
                            await scope.ServiceProvider.GetRequiredService<NotificationEventProcessor>().ProcessAsync(domainEvent, stoppingToken);
                            consumer.Commit(result); // Commit only after all recipients were persisted.
                        }
                        catch (Exception ex) when (ex is ArgumentException or JsonException or KeyNotFoundException or NotificationEventConflictException)
                        {
                            logger.LogWarning(ex, "Persisting rejected notification event at {Offset}.", result.TopicPartitionOffset);
                            using var rejectedScope=scopes.CreateScope();
                            await rejectedScope.ServiceProvider.GetRequiredService<NotificationEventProcessor>().RejectAsync(
                                result.Topic,result.Partition.Value,result.Offset.Value,result.Message.Value,stoppingToken);
                            consumer.Commit(result);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Notification processing failed at {Offset}; retrying without committing.", result.TopicPartitionOffset);
                            consumer.Seek(result.TopicPartitionOffset);
                            await Task.Delay(2000, stoppingToken);
                        }
                    }
                }
                finally { consumer.Close(); }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Notification Kafka consumer unavailable; reconnecting.");
                try { await Task.Delay(2000, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }
}
