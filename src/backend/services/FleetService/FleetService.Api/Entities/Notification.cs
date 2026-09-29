namespace FleetService.Api.Entities;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Guid TargetUserId { get; set; }
    public string Category { get; set; } = "System";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? RelatedEntityId { get; set; }
    public Guid? VehicleId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}
