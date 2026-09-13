using System;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityService.Api.Messaging;

public class UserEventConsumerService : BackgroundService
{
    private readonly KafkaSettings _settings;
    private readonly ILogger<UserEventConsumerService> _logger;

    public UserEventConsumerService(IOptions<KafkaSettings> settings, ILogger<UserEventConsumerService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Kafka background user events consumer is disabled (Kafka:Enabled is false).");
            return;
        }

        // Allow app initialization to complete before spinning up consumer
        await Task.Yield();

        var config = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = _settings.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true,
            SocketTimeoutMs = 5000,
            SessionTimeoutMs = 10000
        };

        try
        {
            using var consumer = new ConsumerBuilder<string, string>(config).Build();
            consumer.Subscribe(_settings.UserEventsTopic);
            _logger.LogInformation("Kafka consumer subscribed to topic '{Topic}' with group '{GroupId}'.", _settings.UserEventsTopic, _settings.ConsumerGroupId);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(TimeSpan.FromMilliseconds(500));
                    if (consumeResult != null && !string.IsNullOrEmpty(consumeResult.Message?.Value))
                    {
                        _logger.LogInformation("[Kafka Consumed] Topic: {Topic}, Key: {Key}, Payload: {Payload}",
                            consumeResult.Topic, consumeResult.Message.Key, consumeResult.Message.Value);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogWarning("Kafka consume warning: {Reason}", ex.Error.Reason);
                    await Task.Delay(2000, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kafka user consumer stopped or broker is currently unreachable at '{BootstrapServers}'.", _settings.BootstrapServers);
        }
    }
}
