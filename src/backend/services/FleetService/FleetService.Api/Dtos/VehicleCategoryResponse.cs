using System;

namespace FleetService.Api.Dtos;

public class VehicleCategoryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int VehicleCount { get; set; }
}
