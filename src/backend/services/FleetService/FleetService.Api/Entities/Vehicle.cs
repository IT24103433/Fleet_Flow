namespace FleetService.Api.Entities;

public class Vehicle
{
    public Guid Id { get; set; }
    public string LicensePlate { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public VehicleStatus Status { get; set; } = VehicleStatus.Available;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Foreign Key and Navigation property
    public Guid VehicleCategoryId { get; set; }
    public VehicleCategory? Category { get; set; }
}
