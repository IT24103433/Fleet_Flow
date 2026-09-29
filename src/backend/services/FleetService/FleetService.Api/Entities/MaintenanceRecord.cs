namespace FleetService.Api.Entities;

public class MaintenanceRecord
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime ScheduledDateTime { get; set; }
    public string ServiceInformation { get; set; } = string.Empty;
    public string? Details { get; set; }
    public decimal Cost { get; set; }
    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.SCHEDULED;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
