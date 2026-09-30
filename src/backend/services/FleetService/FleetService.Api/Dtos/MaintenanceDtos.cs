using FleetService.Api.Entities;
using System.ComponentModel.DataAnnotations;

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

public class CreateMaintenanceRecordRequest
{
    [Required(ErrorMessage = "Vehicle ID is required.")]
    public Guid VehicleId { get; set; }

    [Required(ErrorMessage = "Maintenance date and time is required.")]
    public DateTime ScheduledDateTime { get; set; }

    [Required(ErrorMessage = "Service information is required.")]
    [StringLength(2000, MinimumLength = 3)]
    public string ServiceInformation { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Details { get; set; }

    [Range(0, 100000000)]
    public decimal Cost { get; set; }
}

public class UpdateMaintenanceRecordRequest
{
    [Required(ErrorMessage = "Service information is required.")]
    [StringLength(2000, MinimumLength = 3)]
    public string ServiceInformation { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Details { get; set; }

    [Range(0, 100000000)]
    public decimal Cost { get; set; }
}

public class UpdateMaintenanceStatusRequest
{
    [Required(ErrorMessage = "Maintenance status is required.")]
    public MaintenanceStatus? Status { get; set; }
}

public class MaintenanceRecordResponse
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime ScheduledDateTime { get; set; }
    public string ServiceInformation { get; set; } = string.Empty;
    public string? Details { get; set; }
    public decimal Cost { get; set; }
    public MaintenanceStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string VehicleVin { get; set; } = string.Empty;
    public string VehicleLicensePlate { get; set; } = string.Empty;
    public string VehicleMake { get; set; } = string.Empty;
    public string VehicleModel { get; set; } = string.Empty;
    public int VehicleYear { get; set; }
}
