using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Exceptions;

namespace FleetService.Api.Services;

public class VehicleService : IVehicleService
{
    private readonly FleetDbContext _dbContext;

    public VehicleService(FleetDbContext dbContext)
    {
        _dbContext = dbContext;
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

    public async Task<IEnumerable<VehicleResponse>> GetVehiclesAsync(
        string? category = null,
        VehicleStatus? status = null,
        string? fuel = null,
        string? searchTerm = null,
        int page = 1,
        int pageSize = 20)
    {
        var query = _dbContext.Vehicles
            .AsNoTracking()
            .Include(v => v.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim().ToLower();
            query = query.Where(v => v.Category != null && 
                (v.Category.Name.ToLower() == cat || v.Category.Id.ToString().ToLower() == cat));
        }

        if (status.HasValue)
        {
            query = query.Where(v => v.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(fuel))
        {
            var fuelTrimmed = fuel.Trim().ToLower();
            query = query.Where(v => v.FuelType.ToLower().Contains(fuelTrimmed));
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(v =>
                v.Make.ToLower().Contains(term) ||
                v.Model.ToLower().Contains(term) ||
                v.LicensePlate.ToLower().Contains(term) ||
                v.Vin.ToLower().Contains(term));
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : (pageSize > 100 ? 100 : pageSize);

        var vehicles = await query
            .OrderByDescending(v => v.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return vehicles.Select(MapToResponse);
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
