using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FleetService.Api.Messaging;

public class KafkaProducerService : IKafkaProducerService, IDisposable
{
    private readonly KafkaSettings _settings;
    private readonly ILogger<KafkaProducerService> _logger;
    private readonly Lazy<IProducer<string, string>?> _producer;
    private bool _disposed;

    public KafkaProducerService(IOptions<KafkaSettings> settings, ILogger<KafkaProducerService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _producer = new Lazy<IProducer<string, string>?>(CreateProducer);
    }

    private IProducer<string, string>? CreateProducer()
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Kafka producer is disabled (Kafka:Enabled is false). Messages will not be dispatched to broker.");
            return null;
        }

        try
        {
            var config = new ProducerConfig
            {
                BootstrapServers = _settings.BootstrapServers,
                ClientId = _settings.ClientId,
                Acks = Acks.Leader,
                MessageTimeoutMs = 5000,
                SocketTimeoutMs = 5000
            };

            return new ProducerBuilder<string, string>(config).Build();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize Kafka producer for broker '{BootstrapServers}'. Operating in degraded mode.", _settings.BootstrapServers);
            return null;
        }
    }

    public async Task PublishAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogDebug("Kafka disabled. Skipping event publication for key '{Key}' to topic '{Topic}'.", key, topic);
            return;
        }

        var producer = _producer.Value;
        if (producer == null)
        {
            _logger.LogWarning("Kafka producer is not available. Skipping message to topic '{Topic}'.", topic);
            return;
        }

        try
        {
            var jsonPayload = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            var kafkaMessage = new Message<string, string>
            {
                Key = key,
                Value = jsonPayload
            };

            var deliveryResult = await producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
            _logger.LogInformation("Published Kafka event to '{Topic}' [Partition: {Partition}, Offset: {Offset}]. Key: '{Key}'",
                deliveryResult.Topic, deliveryResult.Partition.Value, deliveryResult.Offset.Value, key);
        }
        catch (Exception ex)
        {
            // Non-blocking: Catch Kafka delivery failure so HTTP requests and DB transactions do not fail
            _logger.LogError(ex, "Failed to publish Kafka event to topic '{Topic}' with key '{Key}'.", topic, key);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_producer.IsValueCreated && _producer.Value != null)
        {
            try
            {
                _producer.Value.Flush(TimeSpan.FromSeconds(2));
                _producer.Value.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while disposing Kafka producer.");
            }
        }
    }
}
