namespace FleetService.Api.Messaging;

public class KafkaSettings
{
    public const string SectionName = "Kafka";

    public bool Enabled { get; set; } = false;
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string ClientId { get; set; } = "FleetService";
    public string VehicleEventsTopic { get; set; } = "fleetflow.vehicle.events";
    public string ConsumerGroupId { get; set; } = "fleetflow.fleet-service.group";
}
