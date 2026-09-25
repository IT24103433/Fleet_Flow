using System;
using FleetService.Api.Entities;

namespace FleetService.Api.Messaging.Events;

public class VehicleCreatedEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = nameof(VehicleCreatedEvent);
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public Guid VehicleId { get; set; }
    public string Vin { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public Guid VehicleCategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal DailyRate { get; set; }
    public string Transmission { get; set; } = string.Empty;
    public string FuelType { get; set; } = string.Empty;
    public string SeatingCapacity { get; set; } = string.Empty;
    public string HubLocation { get; set; } = string.Empty;
    public int Mileage { get; set; }
    public VehicleStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
