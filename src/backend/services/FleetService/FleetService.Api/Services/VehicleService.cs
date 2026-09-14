using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Exceptions;
using FleetService.Api.Messaging;
using FleetService.Api.Messaging.Events;
using Microsoft.Extensions.Options;

namespace FleetService.Api.Services;

public class VehicleService : IVehicleService
{
    private readonly FleetDbContext _dbContext;
    private readonly IKafkaProducerService? _kafkaProducer;
    private readonly KafkaSettings _kafkaSettings;

    public VehicleService(
        FleetDbContext dbContext,
        IKafkaProducerService? kafkaProducer = null,
        IOptions<KafkaSettings>? kafkaSettings = null)
    {
        _dbContext = dbContext;
        _kafkaProducer = kafkaProducer;
        _kafkaSettings = kafkaSettings?.Value ?? new KafkaSettings();
    }

    public async Task<IEnumerable<VehicleCategoryResponse>> GetCategoriesAsync()
    {
        var categories = await _dbContext.VehicleCategories
            .AsNoTracking()
            .Include(c => c.Vehicles)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return categories.Select(c => new VehicleCategoryResponse
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            VehicleCount = c.Vehicles.Count
        });
    }

    public async Task<PagedVehicleResult> GetPagedVehiclesAsync(VehicleQueryParameters parameters)
    {
        parameters ??= new VehicleQueryParameters();

        var query = _dbContext.Vehicles
            .AsNoTracking()
            .Include(v => v.Category)
            .AsQueryable();

        // 1. Search (VIN, license plate, make, model)
        if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            var term = parameters.SearchTerm.Trim().ToLower();
            query = query.Where(v =>
                v.Vin.ToLower().Contains(term) ||
                v.LicensePlate.ToLower().Contains(term) ||
                v.Make.ToLower().Contains(term) ||
                v.Model.ToLower().Contains(term) ||
                (v.Make + " " + v.Model).ToLower().Contains(term));
        }

        // 2. Filters
        // Category
        if (!string.IsNullOrWhiteSpace(parameters.Category) && 
            !parameters.Category.Equals("ALL", StringComparison.OrdinalIgnoreCase) &&
            !parameters.Category.Equals("All Categories", StringComparison.OrdinalIgnoreCase))
        {
            var cat = parameters.Category.Trim().ToLower();
            query = query.Where(v => v.Category != null && 
                (v.Category.Name.ToLower() == cat || v.Category.Id.ToString().ToLower() == cat));
        }

        // Status
        if (parameters.Status.HasValue)
        {
            query = query.Where(v => v.Status == parameters.Status.Value);
        }

        // Fuel Type
        if (!string.IsNullOrWhiteSpace(parameters.Fuel) &&
            !parameters.Fuel.Equals("ALL", StringComparison.OrdinalIgnoreCase) &&
            !parameters.Fuel.Equals("All Fuels", StringComparison.OrdinalIgnoreCase) &&
            !parameters.Fuel.Equals("All Powertrains", StringComparison.OrdinalIgnoreCase))
        {
            var fuelTrimmed = parameters.Fuel.Trim().ToLower();
            query = query.Where(v => v.FuelType.ToLower().Contains(fuelTrimmed));
        }

        // Transmission
        if (!string.IsNullOrWhiteSpace(parameters.Transmission) &&
            !parameters.Transmission.Equals("ALL", StringComparison.OrdinalIgnoreCase) &&
            !parameters.Transmission.Equals("All Transmissions", StringComparison.OrdinalIgnoreCase))
        {
            var transTrimmed = parameters.Transmission.Trim().ToLower();
            query = query.Where(v => v.Transmission.ToLower().Contains(transTrimmed));
        }

        // Hub Location
        if (!string.IsNullOrWhiteSpace(parameters.Hub) &&
            !parameters.Hub.Equals("ALL", StringComparison.OrdinalIgnoreCase) &&
            !parameters.Hub.Equals("All Hubs", StringComparison.OrdinalIgnoreCase) &&
            !parameters.Hub.Equals("All Locations", StringComparison.OrdinalIgnoreCase))
        {
            var hubTrimmed = parameters.Hub.Trim().ToLower();
            query = query.Where(v => v.HubLocation.ToLower().Contains(hubTrimmed));
        }

        // Total count before paging
        var totalCount = await query.CountAsync();

        // 3. Sorting
        var isAscending = string.Equals(parameters.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);
        var sortBy = parameters.SortBy?.Trim().ToLower() ?? "createdat";

        query = sortBy switch
        {
            "make" => isAscending 
                ? query.OrderBy(v => v.Make).ThenBy(v => v.Model)
                : query.OrderByDescending(v => v.Make).ThenByDescending(v => v.Model),
            "model" => isAscending
                ? query.OrderBy(v => v.Model)
                : query.OrderByDescending(v => v.Model),
            "year" => isAscending
                ? query.OrderBy(v => v.Year)
                : query.OrderByDescending(v => v.Year),
            "dailyrate" or "rate" or "price" => isAscending
                ? query.OrderBy(v => v.DailyRate)
                : query.OrderByDescending(v => v.DailyRate),
            "mileage" or "odometer" => isAscending
                ? query.OrderBy(v => v.Mileage)
                : query.OrderByDescending(v => v.Mileage),
            "status" => isAscending
                ? query.OrderBy(v => v.Status)
                : query.OrderByDescending(v => v.Status),
            "vin" => isAscending
                ? query.OrderBy(v => v.Vin)
                : query.OrderByDescending(v => v.Vin),
            "licenseplate" or "plate" => isAscending
                ? query.OrderBy(v => v.LicensePlate)
                : query.OrderByDescending(v => v.LicensePlate),
            _ => isAscending
                ? query.OrderBy(v => v.CreatedAt)
                : query.OrderByDescending(v => v.CreatedAt)
        };

        // 4. Pagination
        var page = parameters.Page < 1 ? 1 : parameters.Page;
        var pageSize = parameters.PageSize < 1 ? 20 : (parameters.PageSize > 100 ? 100 : parameters.PageSize);

        var vehicles = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedVehicleResult
        {
            Items = vehicles.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<VehicleResponse>> GetVehiclesAsync(
        string? category = null,
        VehicleStatus? status = null,
        string? fuel = null,
        string? searchTerm = null,
        int page = 1,
        int pageSize = 20,
        string? transmission = null,
        string? hub = null,
        string? sortBy = null,
        string? sortOrder = null)
    {
        var result = await GetPagedVehiclesAsync(new VehicleQueryParameters
        {
            Category = category,
            Status = status,
            Fuel = fuel,
            SearchTerm = searchTerm,
            Page = page,
            PageSize = pageSize,
            Transmission = transmission,
            Hub = hub,
            SortBy = sortBy,
            SortOrder = sortOrder
        });

        return result.Items;
    }

    public async Task<VehicleResponse?> GetVehicleByIdAsync(Guid id)
    {
        var vehicle = await _dbContext.Vehicles
            .AsNoTracking()
            .Include(v => v.Category)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (vehicle == null)
        {
            return null;
        }

        return MapToResponse(vehicle);
    }

    public async Task<VehicleResponse> CreateVehicleAsync(CreateVehicleRequest request)
    {
        if (request == null)
        {
            throw new ValidationException("Vehicle request payload cannot be null.");
        }

        var vin = request.Vin?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(vin) || vin.Length != 17)
        {
            throw new ValidationException("VIN must be exactly 17 characters.");
        }

        var plate = request.LicensePlate?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(plate))
        {
            throw new ValidationException("License plate is required.");
        }

        var make = request.Make?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(make))
        {
            throw new ValidationException("Make is required.");
        }

        var model = request.Model?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ValidationException("Model is required.");
        }

        var maxYear = DateTime.UtcNow.Year + 2;
        if (request.Year < 1900 || request.Year > maxYear)
        {
            throw new ValidationException($"Year must be between 1900 and {maxYear}.");
        }

        if (request.DailyRate <= 0)
        {
            throw new ValidationException("Daily rate must be greater than 0.");
        }

        if (request.Mileage < 0)
        {
            throw new ValidationException("Mileage must be non-negative.");
        }

        if (string.IsNullOrWhiteSpace(request.Transmission))
        {
            throw new ValidationException("Transmission is required.");
        }

        if (string.IsNullOrWhiteSpace(request.FuelType))
        {
            throw new ValidationException("Fuel type is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SeatingCapacity))
        {
            throw new ValidationException("Seating capacity is required.");
        }

        if (string.IsNullOrWhiteSpace(request.HubLocation))
        {
            throw new ValidationException("Hub location is required.");
        }

        var category = await _dbContext.VehicleCategories
            .FirstOrDefaultAsync(c => c.Id == request.VehicleCategoryId);

        if (category == null)
        {
            throw new ValidationException($"Vehicle category with ID '{request.VehicleCategoryId}' does not exist.");
        }

        var vinExists = await _dbContext.Vehicles
            .AnyAsync(v => v.Vin.ToLower() == vin.ToLower());

        if (vinExists)
        {
            throw new DuplicateException($"A vehicle with VIN '{vin}' already exists.");
        }

        var plateExists = await _dbContext.Vehicles
            .AnyAsync(v => v.LicensePlate.ToLower() == plate.ToLower());

        if (plateExists)
        {
            throw new DuplicateException($"A vehicle with license plate '{plate}' already exists.");
        }

        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            Vin = vin.ToUpperInvariant(),
            LicensePlate = plate.ToUpperInvariant(),
            Make = make,
            Model = model,
            Year = request.Year,
            VehicleCategoryId = category.Id,
            Category = category,
            DailyRate = request.DailyRate,
            Transmission = request.Transmission.Trim(),
            FuelType = request.FuelType.Trim(),
            SeatingCapacity = request.SeatingCapacity.Trim(),
            HubLocation = request.HubLocation.Trim(),
            Mileage = request.Mileage,
            Status = VehicleStatus.Available,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Vehicles.Add(vehicle);
        await _dbContext.SaveChangesAsync();

        if (_kafkaProducer != null)
        {
            var createdEvent = new VehicleCreatedEvent
            {
                VehicleId = vehicle.Id,
                Vin = vehicle.Vin,
                LicensePlate = vehicle.LicensePlate,
                Make = vehicle.Make,
                Model = vehicle.Model,
                Year = vehicle.Year,
                VehicleCategoryId = vehicle.VehicleCategoryId,
                CategoryName = category.Name,
                DailyRate = vehicle.DailyRate,
                Transmission = vehicle.Transmission,
                FuelType = vehicle.FuelType,
                SeatingCapacity = vehicle.SeatingCapacity,
                HubLocation = vehicle.HubLocation,
                Mileage = vehicle.Mileage,
                Status = vehicle.Status,
                CreatedAt = vehicle.CreatedAt
            };

            _ = _kafkaProducer.PublishAsync(_kafkaSettings.VehicleEventsTopic, vehicle.Id.ToString(), createdEvent);
        }

        return MapToResponse(vehicle);
    }

    public async Task<VehicleResponse> UpdateVehicleAsync(Guid id, UpdateVehicleRequest request)
    {
        if (request == null)
        {
            throw new ValidationException("Vehicle request payload cannot be null.");
        }

        var vehicle = await _dbContext.Vehicles
            .Include(v => v.Category)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (vehicle == null)
        {
            throw new NotFoundException($"Vehicle with ID '{id}' was not found.");
        }

        var vin = request.Vin?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(vin) || vin.Length != 17)
        {
            throw new ValidationException("VIN must be exactly 17 characters.");
        }

        var plate = request.LicensePlate?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(plate))
        {
            throw new ValidationException("License plate is required.");
        }

        var make = request.Make?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(make))
        {
            throw new ValidationException("Make is required.");
        }

        var model = request.Model?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ValidationException("Model is required.");
        }

        var maxYear = DateTime.UtcNow.Year + 2;
        if (request.Year < 1900 || request.Year > maxYear)
        {
            throw new ValidationException($"Year must be between 1900 and {maxYear}.");
        }

        if (request.DailyRate <= 0)
        {
            throw new ValidationException("Daily rate must be greater than 0.");
        }

        if (request.Mileage < 0)
        {
            throw new ValidationException("Mileage must be non-negative.");
        }

        if (string.IsNullOrWhiteSpace(request.Transmission))
        {
            throw new ValidationException("Transmission is required.");
        }

        if (string.IsNullOrWhiteSpace(request.FuelType))
        {
            throw new ValidationException("Fuel type is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SeatingCapacity))
        {
            throw new ValidationException("Seating capacity is required.");
        }

        if (string.IsNullOrWhiteSpace(request.HubLocation))
        {
            throw new ValidationException("Hub location is required.");
        }

        var category = await _dbContext.VehicleCategories
            .FirstOrDefaultAsync(c => c.Id == request.VehicleCategoryId);

        if (category == null)
        {
            throw new ValidationException($"Vehicle category with ID '{request.VehicleCategoryId}' does not exist.");
        }

        var vinExists = await _dbContext.Vehicles
            .AnyAsync(v => v.Vin.ToLower() == vin.ToLower() && v.Id != id);

        if (vinExists)
        {
            throw new DuplicateException($"A vehicle with VIN '{vin}' already exists.");
        }

        var plateExists = await _dbContext.Vehicles
            .AnyAsync(v => v.LicensePlate.ToLower() == plate.ToLower() && v.Id != id);

        if (plateExists)
        {
            throw new DuplicateException($"A vehicle with license plate '{plate}' already exists.");
        }

        vehicle.Vin = vin.ToUpperInvariant();
        vehicle.LicensePlate = plate.ToUpperInvariant();
        vehicle.Make = make;
        vehicle.Model = model;
        vehicle.Year = request.Year;
        vehicle.VehicleCategoryId = category.Id;
        vehicle.Category = category;
        vehicle.DailyRate = request.DailyRate;
        vehicle.Transmission = request.Transmission.Trim();
        vehicle.FuelType = request.FuelType.Trim();
        vehicle.SeatingCapacity = request.SeatingCapacity.Trim();
        vehicle.HubLocation = request.HubLocation.Trim();
        vehicle.Mileage = request.Mileage;
        vehicle.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        if (_kafkaProducer != null)
        {
            var updatedEvent = new VehicleUpdatedEvent
            {
                VehicleId = vehicle.Id,
                Vin = vehicle.Vin,
                LicensePlate = vehicle.LicensePlate,
                Make = vehicle.Make,
                Model = vehicle.Model,
                Year = vehicle.Year,
                VehicleCategoryId = vehicle.VehicleCategoryId,
                CategoryName = category.Name,
                DailyRate = vehicle.DailyRate,
                Transmission = vehicle.Transmission,
                FuelType = vehicle.FuelType,
                SeatingCapacity = vehicle.SeatingCapacity,
                HubLocation = vehicle.HubLocation,
                Mileage = vehicle.Mileage,
                Status = vehicle.Status,
                UpdatedAt = vehicle.UpdatedAt
            };

            _ = _kafkaProducer.PublishAsync(_kafkaSettings.VehicleEventsTopic, vehicle.Id.ToString(), updatedEvent);
        }

        return MapToResponse(vehicle);
    }

    public async Task<VehicleResponse> UpdateVehicleStatusAsync(Guid id, UpdateVehicleStatusRequest request)
    {
        if (request == null)
        {
            throw new ValidationException("Status request payload cannot be null.");
        }

        if (!Enum.IsDefined(typeof(VehicleStatus), request.Status))
        {
            throw new ValidationException($"Invalid vehicle status '{request.Status}'. Supported statuses are: {string.Join(", ", Enum.GetNames<VehicleStatus>())}.");
        }

        var vehicle = await _dbContext.Vehicles
            .Include(v => v.Category)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (vehicle == null)
        {
            throw new NotFoundException($"Vehicle with ID '{id}' was not found.");
        }

        vehicle.Status = request.Status;
        vehicle.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        if (_kafkaProducer != null)
        {
            var updatedEvent = new VehicleUpdatedEvent
            {
                VehicleId = vehicle.Id,
                Vin = vehicle.Vin,
                LicensePlate = vehicle.LicensePlate,
                Make = vehicle.Make,
                Model = vehicle.Model,
                Year = vehicle.Year,
                VehicleCategoryId = vehicle.VehicleCategoryId,
                CategoryName = vehicle.Category?.Name ?? string.Empty,
                DailyRate = vehicle.DailyRate,
                Transmission = vehicle.Transmission,
                FuelType = vehicle.FuelType,
                SeatingCapacity = vehicle.SeatingCapacity,
                HubLocation = vehicle.HubLocation,
                Mileage = vehicle.Mileage,
                Status = vehicle.Status,
                UpdatedAt = vehicle.UpdatedAt
            };

            _ = _kafkaProducer.PublishAsync(_kafkaSettings.VehicleEventsTopic, vehicle.Id.ToString(), updatedEvent);
        }

        return MapToResponse(vehicle);
    }

    private static VehicleResponse MapToResponse(Vehicle vehicle)
    {
        return new VehicleResponse
        {
            Id = vehicle.Id,
            Vin = vehicle.Vin,
            LicensePlate = vehicle.LicensePlate,
            Make = vehicle.Make,
            Model = vehicle.Model,
            Year = vehicle.Year,
            CategoryName = vehicle.Category?.Name ?? string.Empty,
            VehicleCategoryId = vehicle.VehicleCategoryId,
            DailyRate = vehicle.DailyRate,
            Transmission = vehicle.Transmission,
            FuelType = vehicle.FuelType,
            SeatingCapacity = vehicle.SeatingCapacity,
            HubLocation = vehicle.HubLocation,
            Mileage = vehicle.Mileage,
            Status = vehicle.Status,
            CreatedAt = vehicle.CreatedAt,
            UpdatedAt = vehicle.UpdatedAt
        };
    }
}
