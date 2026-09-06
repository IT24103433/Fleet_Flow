using System;
using System.ComponentModel.DataAnnotations;

namespace FleetService.Api.Dtos;

public class CreateVehicleRequest
{
    [Required(ErrorMessage = "VIN is required.")]
    [StringLength(17, MinimumLength = 17, ErrorMessage = "VIN must be exactly 17 characters.")]
    public string Vin { get; set; } = string.Empty;

    [Required(ErrorMessage = "License plate is required.")]
    [StringLength(20, ErrorMessage = "License plate cannot exceed 20 characters.")]
    public string LicensePlate { get; set; } = string.Empty;

    [Required(ErrorMessage = "Make is required.")]
    [StringLength(50, ErrorMessage = "Make cannot exceed 50 characters.")]
    public string Make { get; set; } = string.Empty;

    [Required(ErrorMessage = "Model is required.")]
    [StringLength(50, ErrorMessage = "Model cannot exceed 50 characters.")]
    public string Model { get; set; } = string.Empty;

    [Required(ErrorMessage = "Year is required.")]
    [Range(1900, 2100, ErrorMessage = "Year must be a valid year between 1900 and 2100.")]
    public int Year { get; set; }

    [Required(ErrorMessage = "VehicleCategoryId is required.")]
    public Guid VehicleCategoryId { get; set; }

    [Required(ErrorMessage = "Daily rate is required.")]
    [Range(0.01, 100000.00, ErrorMessage = "Daily rate must be greater than 0.")]
    public decimal DailyRate { get; set; }

    [Required(ErrorMessage = "Transmission is required.")]
    [StringLength(50, ErrorMessage = "Transmission cannot exceed 50 characters.")]
    public string Transmission { get; set; } = string.Empty;

    [Required(ErrorMessage = "Fuel type is required.")]
    [StringLength(50, ErrorMessage = "Fuel type cannot exceed 50 characters.")]
    public string FuelType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Seating capacity is required.")]
    [StringLength(50, ErrorMessage = "Seating capacity cannot exceed 50 characters.")]
    public string SeatingCapacity { get; set; } = string.Empty;

    [Required(ErrorMessage = "Hub location is required.")]
    [StringLength(100, ErrorMessage = "Hub location cannot exceed 100 characters.")]
    public string HubLocation { get; set; } = string.Empty;

    [Range(0, int.MaxValue, ErrorMessage = "Mileage must be non-negative.")]
    public int Mileage { get; set; }
}
