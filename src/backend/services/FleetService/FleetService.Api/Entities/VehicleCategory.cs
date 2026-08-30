namespace FleetService.Api.Entities;

public class VehicleCategory
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Navigation property for one-to-many relationship
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
