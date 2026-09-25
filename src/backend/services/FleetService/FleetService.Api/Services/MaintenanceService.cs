using Microsoft.EntityFrameworkCore;
using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;

namespace FleetService.Api.Services;

public class MaintenanceService : IMaintenanceService
{
    private readonly FleetDbContext _dbContext;

    public MaintenanceService(FleetDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<MaintenanceDashboardResponse> GetMaintenanceDashboardAsync(string? hub = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Vehicles
            .AsNoTracking()
            .Include(v => v.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(hub) &&
            !hub.Equals("ALL", StringComparison.OrdinalIgnoreCase) &&
            !hub.Equals("All Hubs", StringComparison.OrdinalIgnoreCase) &&
            !hub.Equals("All Locations", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedHub = hub.Trim().ToLower();
            query = query.Where(v => v.HubLocation.ToLower() == normalizedHub);
        }

        var allVehicles = await query.ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;

        // 1. Compute Exact Persisted Status Counts (Zero Fabricated Metrics)
        var statusCounts = new MaintenanceStatusCountsDto
        {
            TotalVehicles = allVehicles.Count,
            UndergoingMaintenance = allVehicles.Count(v => v.Status == VehicleStatus.Maintenance),
            Available = allVehicles.Count(v => v.Status == VehicleStatus.Available),
            InUse = allVehicles.Count(v => v.Status == VehicleStatus.InUse),
            Retired = allVehicles.Count(v => v.Status == VehicleStatus.Retired)
        };

        // 2. Identify Vehicles Requiring Attention
        var attentionItems = new List<MaintenanceAttentionItemDto>();

        foreach (var vehicle in allVehicles)
        {
            var lastStatusChange = vehicle.UpdatedAt ?? vehicle.CreatedAt;
            var daysInState = Math.Max(0, (int)(now - lastStatusChange).TotalDays);

            if (vehicle.Status == VehicleStatus.Maintenance)
            {
                string reason;
                string urgency;

                if (daysInState >= 7)
                {
                    reason = $"Extended Service ({daysInState} days in maintenance)";
                    urgency = "High";
                }
                else if (vehicle.Mileage >= 50000)
                {
                    reason = $"Active Maintenance & High Odometer ({vehicle.Mileage:N0} mi)";
                    urgency = "High";
                }
                else
                {
                    reason = "Undergoing Active Maintenance";
                    urgency = "Medium";
                }

                attentionItems.Add(new MaintenanceAttentionItemDto
                {
                    Id = vehicle.Id,
                    Vin = vehicle.Vin,
                    LicensePlate = vehicle.LicensePlate,
                    Make = vehicle.Make,
                    Model = vehicle.Model,
                    Year = vehicle.Year,
                    HubLocation = vehicle.HubLocation,
                    Mileage = vehicle.Mileage,
                    Status = vehicle.Status,
                    AttentionReason = reason,
                    Urgency = urgency,
                    DaysInMaintenance = daysInState
                });
            }
            else if (vehicle.Status == VehicleStatus.Available && vehicle.Mileage >= 50000)
            {
                var urgency = vehicle.Mileage >= 80000 ? "High" : "Medium";
                attentionItems.Add(new MaintenanceAttentionItemDto
                {
                    Id = vehicle.Id,
                    Vin = vehicle.Vin,
                    LicensePlate = vehicle.LicensePlate,
                    Make = vehicle.Make,
                    Model = vehicle.Model,
                    Year = vehicle.Year,
                    HubLocation = vehicle.HubLocation,
                    Mileage = vehicle.Mileage,
                    Status = vehicle.Status,
                    AttentionReason = $"Routine Preventive Service Recommended ({vehicle.Mileage:N0} mi)",
                    Urgency = urgency,
                    DaysInMaintenance = 0
                });
            }
        }

        // Sort attention items: High urgency first, then by days in maintenance descending, then mileage descending
        var sortedAttentionItems = attentionItems
            .OrderBy(a => a.Urgency == "High" ? 0 : a.Urgency == "Medium" ? 1 : 2)
            .ThenByDescending(a => a.DaysInMaintenance)
            .ThenByDescending(a => a.Mileage)
            .ToList();

        // 3. Extract Maintenance Vehicles List
        var maintenanceVehicles = allVehicles
            .Where(v => v.Status == VehicleStatus.Maintenance)
            .OrderByDescending(v => v.UpdatedAt ?? v.CreatedAt)
            .Select(v => new MaintenanceItemDto
            {
                Id = v.Id,
                Vin = v.Vin,
                LicensePlate = v.LicensePlate,
                Make = v.Make,
                Model = v.Model,
                Year = v.Year,
                CategoryName = v.Category?.Name ?? "Uncategorized",
                Mileage = v.Mileage,
                HubLocation = v.HubLocation,
                Status = v.Status,
                CreatedAt = v.CreatedAt,
                UpdatedAt = v.UpdatedAt,
                DaysInCurrentStatus = Math.Max(0, (int)(now - (v.UpdatedAt ?? v.CreatedAt)).TotalDays)
            })
            .ToList();

        return new MaintenanceDashboardResponse
        {
            StatusCounts = statusCounts,
            AttentionItems = sortedAttentionItems,
            MaintenanceVehicles = maintenanceVehicles,
            GeneratedAt = now
        };
    }
}
