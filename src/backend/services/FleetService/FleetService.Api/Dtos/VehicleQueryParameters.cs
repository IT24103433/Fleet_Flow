using FleetService.Api.Entities;

namespace FleetService.Api.Dtos;

public class VehicleQueryParameters
{
    public string? SearchTerm { get; set; }
    public string? Category { get; set; }
    public VehicleStatus? Status { get; set; }
    public string? Fuel { get; set; }
    public string? Transmission { get; set; }
    public string? Hub { get; set; }
    public string? SortBy { get; set; }
    public string? SortOrder { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public bool Paged { get; set; } = false;
}
