using Microsoft.EntityFrameworkCore;
using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Messaging;
using FleetService.Api.Messaging.Events;

namespace FleetService.Api.Services;

public class MaintenanceService : IMaintenanceService
{
    private readonly FleetDbContext _dbContext;
    private readonly INotificationEventDispatcher? _notifications;
    private readonly ILogger<MaintenanceService>? _logger;

    public MaintenanceService(
        FleetDbContext dbContext,
        INotificationEventDispatcher? notifications = null,
        ILogger<MaintenanceService>? logger = null)
    {
        _dbContext = dbContext;
        _notifications = notifications;
        _logger = logger;
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

    public async Task<MaintenanceRecordResponse> CreateMaintenanceRecordAsync(
        Guid createdByUserId,
        CreateMaintenanceRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (createdByUserId == Guid.Empty)
        {
            throw new Exceptions.ValidationException("Authenticated staff identity is required.");
        }

        if (request == null)
        {
            throw new Exceptions.ValidationException("Maintenance request is required.");
        }

        ValidateRecordDetails(request.ServiceInformation, request.Details, request.Cost);
        var scheduledUtc = NormalizeUtc(request.ScheduledDateTime);
        if (scheduledUtc == default || scheduledUtc < DateTime.UtcNow.AddMinutes(-5))
        {
            throw new Exceptions.ValidationException("Maintenance date and time must be current or in the future.");
        }

        var vehicle = await _dbContext.Vehicles
            .FirstOrDefaultAsync(v => v.Id == request.VehicleId, cancellationToken);
        if (vehicle == null)
        {
            throw new Exceptions.NotFoundException($"Vehicle with ID '{request.VehicleId}' was not found.");
        }

        if (vehicle.Status is VehicleStatus.InUse or VehicleStatus.Retired)
        {
            throw new Exceptions.ValidationException($"Vehicle in '{vehicle.Status}' status cannot be scheduled for maintenance.");
        }

        var hasActiveRecord = await _dbContext.MaintenanceRecords.AnyAsync(
            record => record.VehicleId == vehicle.Id &&
                      (record.Status == MaintenanceStatus.SCHEDULED || record.Status == MaintenanceStatus.IN_PROGRESS),
            cancellationToken);
        if (hasActiveRecord)
        {
            throw new Exceptions.DuplicateException("This vehicle already has an active maintenance record.");
        }

        var record = new MaintenanceRecord
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicle.Id,
            Vehicle = vehicle,
            CreatedByUserId = createdByUserId,
            ScheduledDateTime = scheduledUtc,
            ServiceInformation = request.ServiceInformation.Trim(),
            Details = NormalizeOptionalText(request.Details),
            Cost = request.Cost,
            Status = MaintenanceStatus.SCHEDULED,
            CreatedAt = DateTime.UtcNow
        };

        vehicle.Status = VehicleStatus.Maintenance;
        vehicle.UpdatedAt = DateTime.UtcNow;
        _dbContext.MaintenanceRecords.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TryDispatch(new MaintenanceScheduledEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAt = record.CreatedAt,
            MaintenanceId = record.Id,
            VehicleId = record.VehicleId,
            Activity = record.ServiceInformation,
            TargetUserIds = [record.CreatedByUserId]
        }, record.Id);

        return MapRecord(record);
    }

    public async Task<MaintenanceRecordResponse?> GetMaintenanceRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.MaintenanceRecords
            .AsNoTracking()
            .Include(item => item.Vehicle)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        return record == null ? null : MapRecord(record);
    }

    public async Task<IReadOnlyList<MaintenanceRecordResponse>> GetVehicleMaintenanceHistoryAsync(
        Guid vehicleId,
        CancellationToken cancellationToken = default)
    {
        if (vehicleId == Guid.Empty)
        {
            throw new Exceptions.ValidationException("Vehicle ID is required.");
        }

        var vehicleExists = await _dbContext.Vehicles.AnyAsync(v => v.Id == vehicleId, cancellationToken);
        if (!vehicleExists)
        {
            throw new Exceptions.NotFoundException($"Vehicle with ID '{vehicleId}' was not found.");
        }

        var records = await _dbContext.MaintenanceRecords
            .AsNoTracking()
            .Include(item => item.Vehicle)
            .Where(item => item.VehicleId == vehicleId)
            .OrderByDescending(item => item.ScheduledDateTime)
            .ThenByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return records.Select(MapRecord).ToList();
    }

    public async Task<MaintenanceRecordResponse?> UpdateMaintenanceRecordAsync(
        Guid id,
        UpdateMaintenanceRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new Exceptions.ValidationException("Maintenance update request is required.");
        }

        ValidateRecordDetails(request.ServiceInformation, request.Details, request.Cost);
        var record = await _dbContext.MaintenanceRecords
            .Include(item => item.Vehicle)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (record == null)
        {
            return null;
        }

        record.ServiceInformation = request.ServiceInformation.Trim();
        record.Details = NormalizeOptionalText(request.Details);
        record.Cost = request.Cost;
        record.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapRecord(record);
    }

    public async Task<MaintenanceRecordResponse?> UpdateMaintenanceStatusAsync(
        Guid id,
        MaintenanceStatus status,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(status))
        {
            throw new Exceptions.ValidationException("Invalid maintenance status.");
        }

        var record = await _dbContext.MaintenanceRecords
            .Include(item => item.Vehicle)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (record == null)
        {
            return null;
        }

        if (!IsValidTransition(record.Status, status))
        {
            throw new Exceptions.ValidationException($"Maintenance status cannot transition from '{record.Status}' to '{status}'.");
        }

        record.Status = status;
        record.UpdatedAt = DateTime.UtcNow;

        if (status == MaintenanceStatus.COMPLETED)
        {
            record.CompletedAt = DateTime.UtcNow;
        }

        if (record.Vehicle != null)
        {
            if (status is MaintenanceStatus.SCHEDULED or MaintenanceStatus.IN_PROGRESS)
            {
                record.Vehicle.Status = VehicleStatus.Maintenance;
                record.Vehicle.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var hasOtherActiveRecord = await _dbContext.MaintenanceRecords.AnyAsync(
                    item => item.VehicleId == record.VehicleId && item.Id != record.Id &&
                            (item.Status == MaintenanceStatus.SCHEDULED || item.Status == MaintenanceStatus.IN_PROGRESS),
                    cancellationToken);

                if (!hasOtherActiveRecord && record.Vehicle.Status == VehicleStatus.Maintenance)
                {
                    record.Vehicle.Status = VehicleStatus.Available;
                    record.Vehicle.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        TryDispatch(new MaintenanceStatusChangedEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAt = record.UpdatedAt!.Value,
            MaintenanceId = record.Id,
            VehicleId = record.VehicleId,
            Activity = record.ServiceInformation,
            Status = record.Status.ToString(),
            TargetUserIds = [record.CreatedByUserId]
        }, record.Id);
        return MapRecord(record);
    }

    private void TryDispatch(NotificationDomainEvent domainEvent, Guid maintenanceId)
    {
        try
        {
            _notifications?.TryEnqueue(domainEvent);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "Maintenance record {MaintenanceId} persisted but notification dispatch failed.", maintenanceId);
        }
    }

    private static bool IsValidTransition(MaintenanceStatus current, MaintenanceStatus target)
    {
        return current switch
        {
            MaintenanceStatus.SCHEDULED => target is MaintenanceStatus.IN_PROGRESS or MaintenanceStatus.CANCELLED,
            MaintenanceStatus.IN_PROGRESS => target is MaintenanceStatus.COMPLETED or MaintenanceStatus.CANCELLED,
            _ => false
        };
    }

    private static void ValidateRecordDetails(string serviceInformation, string? details, decimal cost)
    {
        if (string.IsNullOrWhiteSpace(serviceInformation) || serviceInformation.Trim().Length < 3)
        {
            throw new Exceptions.ValidationException("Service information must contain at least 3 characters.");
        }

        if (serviceInformation.Trim().Length > 2000 || details?.Trim().Length > 4000)
        {
            throw new Exceptions.ValidationException("Maintenance service information or details exceed the supported length.");
        }

        if (cost < 0)
        {
            throw new Exceptions.ValidationException("Maintenance cost cannot be negative.");
        }
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static MaintenanceRecordResponse MapRecord(MaintenanceRecord record)
    {
        return new MaintenanceRecordResponse
        {
            Id = record.Id,
            VehicleId = record.VehicleId,
            CreatedByUserId = record.CreatedByUserId,
            ScheduledDateTime = record.ScheduledDateTime,
            ServiceInformation = record.ServiceInformation,
            Details = record.Details,
            Cost = record.Cost,
            Status = record.Status,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt,
            CompletedAt = record.CompletedAt,
            VehicleVin = record.Vehicle?.Vin ?? string.Empty,
            VehicleLicensePlate = record.Vehicle?.LicensePlate ?? string.Empty,
            VehicleMake = record.Vehicle?.Make ?? string.Empty,
            VehicleModel = record.Vehicle?.Model ?? string.Empty,
            VehicleYear = record.Vehicle?.Year ?? 0
        };
    }
}
