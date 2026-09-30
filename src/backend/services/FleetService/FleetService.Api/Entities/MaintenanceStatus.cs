using System.Text.Json.Serialization;

namespace FleetService.Api.Entities;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenanceStatus
{
    SCHEDULED,
    IN_PROGRESS,
    COMPLETED,
    CANCELLED
}
