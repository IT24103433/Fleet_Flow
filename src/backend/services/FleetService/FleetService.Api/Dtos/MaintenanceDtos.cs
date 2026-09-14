using FleetService.Api.Entities;

namespace FleetService.Api.Dtos;

public class MaintenanceStatusCountsDto
{
    public int TotalVehicles { get; set; }
    public int UndergoingMaintenance { get; set; }
    public int Available { get; set; }
    public int InUse { get; set; }
    public int Retired { get; set; }
}

public class MaintenanceItemDto
{
    public Guid Id { get; set; }
    public string Vin { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int Mileage { get; set; }
    public string HubLocation { get; set; } = string.Empty;
    public VehicleStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int DaysInCurrentStatus { get; set; }
}

public class MaintenanceAttentionItemDto
{
    public Guid Id { get; set; }
    public string Vin { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string HubLocation { get; set; } = string.Empty;
    public int Mileage { get; set; }
    public VehicleStatus Status { get; set; }
    public string AttentionReason { get; set; } = string.Empty;
    public string Urgency { get; set; } = "Normal";
    public int DaysInMaintenance { get; set; }
}

public class MaintenanceDashboardResponse
{
    public MaintenanceStatusCountsDto StatusCounts { get; set; } = new();
    public List<MaintenanceAttentionItemDto> AttentionItems { get; set; } = new();
    public List<MaintenanceItemDto> MaintenanceVehicles { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
