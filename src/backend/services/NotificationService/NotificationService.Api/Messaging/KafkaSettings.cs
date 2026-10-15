using Confluent.Kafka;

namespace NotificationService.Api.Messaging;

public class KafkaSettings
{
    public const string SectionName = "Kafka";

    public bool Enabled { get; set; } = false;
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string ClientId { get; set; } = "NotificationService";
    public string VehicleEventsTopic { get; set; } = "fleetflow.vehicle.events";
    public string ConsumerGroupId { get; set; } = "fleetflow.fleet-service.group";
    public string BookingEventsTopic { get; set; } = "fleetflow.booking.events";
    public string MaintenanceEventsTopic { get; set; } = "fleetflow.maintenance.events";
    public string NotificationConsumerGroupId { get; set; } = "fleetflow.notifications.group";

    public string? SecurityProtocol { get; set; }
    public string? SaslMechanism { get; set; }
    public string? SaslUsername { get; set; }
    public string? SaslPassword { get; set; }
    public string? SslCaLocation { get; set; }

    public void ApplyClientConfig(ClientConfig config)
    {
        if (!string.IsNullOrEmpty(SecurityProtocol) &&
            Enum.TryParse<SecurityProtocol>(SecurityProtocol, true, out var parsedProtocol))
        {
            config.SecurityProtocol = parsedProtocol;
        }

        if (!string.IsNullOrEmpty(SaslMechanism) &&
            Enum.TryParse<SaslMechanism>(SaslMechanism, true, out var parsedSasl))
        {
            config.SaslMechanism = parsedSasl;
        }

        if (!string.IsNullOrEmpty(SaslUsername))
        {
            config.SaslUsername = SaslUsername;
        }

        if (!string.IsNullOrEmpty(SaslPassword))
        {
            config.SaslPassword = SaslPassword;
        }

        if (!string.IsNullOrEmpty(SslCaLocation))
        {
            config.SslCaLocation = SslCaLocation;
        }
    }
}
