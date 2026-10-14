using System.Text.Json.Serialization;

namespace FleetService.Api.Entities;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VehicleStatus
{
    Available,
    InUse,
    Maintenance,
    Retired
}
