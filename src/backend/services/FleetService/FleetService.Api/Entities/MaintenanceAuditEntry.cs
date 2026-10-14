namespace FleetService.Api.Entities;
public sealed class MaintenanceAuditEntry
{
    public Guid Id { get; set; }
    public Guid MaintenanceRecordId { get; set; }
    public Guid ActorId { get; set; }
    public DateTime OccurredAt { get; set; }
    public string Action { get; set; } = "";
    public string? Before { get; set; }
    public string After { get; set; } = "";
}
