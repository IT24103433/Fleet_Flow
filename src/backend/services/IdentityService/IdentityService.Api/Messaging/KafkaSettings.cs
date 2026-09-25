namespace IdentityService.Api.Messaging;

public class KafkaSettings
{
    public const string SectionName = "Kafka";

    public bool Enabled { get; set; } = false;
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string ClientId { get; set; } = "IdentityService";
    public string UserEventsTopic { get; set; } = "fleetflow.user.events";
    public string ConsumerGroupId { get; set; } = "fleetflow.identity-service.group";
}
