using System.Text.Json.Serialization;

namespace FleetService.Api.Entities;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BookingStatus
{
    Pending,
    Confirmed,
    Cancelled,
    Completed
}
