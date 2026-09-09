namespace FleetService.Api.Entities;

public class Vehicle
{
    public Guid Id { get; set; }
    public string Vin { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal DailyRate { get; set; }
    public string Transmission { get; set; } = string.Empty;
    public string FuelType { get; set; } = string.Empty;
    public string SeatingCapacity { get; set; } = string.Empty;
    public string HubLocation { get; set; } = string.Empty;
    public int Mileage { get; set; }
    public VehicleStatus Status { get; set; } = VehicleStatus.Available;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Foreign Key and Navigation property
    public Guid VehicleCategoryId { get; set; }
    public VehicleCategory? Category { get; set; }

    // Images navigation property
    public ICollection<VehicleImage> Images { get; set; } = new List<VehicleImage>();
}
