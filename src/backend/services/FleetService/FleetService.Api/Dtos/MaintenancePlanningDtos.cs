using System.Text.Json.Serialization;
namespace FleetService.Api.Dtos;
public sealed class MaintenanceScheduleRequest
{
    [JsonRequired] public DateTime ScheduledDateTime { get; set; }
    [JsonRequired] public DateTime PlannedEndDateTime { get; set; }
}
public sealed class MaintenanceAssignmentRequest
{
    [JsonRequired] public Guid? AssignedStaffId { get; set; }
}
