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

public class BookingService : IBookingService
{
    private readonly FleetDbContext _dbContext;

    public BookingService(FleetDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BookingResponse> CreateBookingAsync(Guid customerId, CreateBookingRequest request)
    {
        if (customerId == Guid.Empty)
        {
            throw new ValidationException("Customer ID is required.");
        }

        var startUtc = request.StartDateTime.Kind == DateTimeKind.Utc
            ? request.StartDateTime
            : DateTime.SpecifyKind(request.StartDateTime, DateTimeKind.Utc);
        var endUtc = request.EndDateTime.Kind == DateTimeKind.Utc
            ? request.EndDateTime
            : DateTime.SpecifyKind(request.EndDateTime, DateTimeKind.Utc);

        // 1. Date Validation: StartDateTime must be in future (or current time window)
        if (startUtc < DateTime.UtcNow.AddMinutes(-5))
        {
            throw new ValidationException("Start date and time must be in the future.");
        }

        // 2. Date Validation: EndDateTime must be strictly after StartDateTime
        if (endUtc <= startUtc)
        {
            throw new ValidationException("End date and time must be after start date and time.");
        }

        // 3. Vehicle existence check
        var vehicle = await _dbContext.Vehicles
            .Include(v => v.Category)
            .FirstOrDefaultAsync(v => v.Id == request.VehicleId);

        if (vehicle == null)
        {
            throw new NotFoundException($"Vehicle with ID '{request.VehicleId}' was not found.");
        }

        // 4. Vehicle Status Check: Vehicle must be active / available
        if (vehicle.Status != VehicleStatus.Available)
        {
            throw new ValidationException($"Vehicle is currently in '{vehicle.Status}' status and is not available for rental booking.");
        }

        // 5. Vehicle Availability Check: Verify no overlapping active bookings
        var isAvailable = await CheckVehicleAvailabilityAsync(request.VehicleId, startUtc, endUtc);
        if (!isAvailable)
        {
            throw new DuplicateException("The selected vehicle has an overlapping booking for the requested timeframe.");
        }

        // 6. Calculate total cost based on daily rate
        var durationHours = Math.Round((endUtc - startUtc).TotalHours, 4);
        var totalDays = (decimal)Math.Max(1, Math.Ceiling(durationHours / 24.0));
        var totalCost = Math.Round(totalDays * vehicle.DailyRate, 2);

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            VehicleId = request.VehicleId,
            StartDateTime = startUtc,
            EndDateTime = endUtc,
            Status = request.Status ?? BookingStatus.Confirmed,
            TotalCost = totalCost,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Bookings.Add(booking);
        await _dbContext.SaveChangesAsync();

        return MapToResponse(booking, vehicle);
    }

    public async Task<BookingResponse?> GetBookingByIdAsync(Guid id)
    {
        var booking = await _dbContext.Bookings
            .Include(b => b.Vehicle)
            .ThenInclude(v => v!.Category)
            .FirstOrDefaultAsync(b => b.Id == id);

        return booking == null ? null : MapToResponse(booking, booking.Vehicle);
    }

    public async Task<IEnumerable<BookingResponse>> GetCustomerBookingsAsync(Guid customerId)
    {
        var bookings = await _dbContext.Bookings
            .Include(b => b.Vehicle)
            .ThenInclude(v => v!.Category)
            .Where(b => b.CustomerId == customerId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return bookings.Select(b => MapToResponse(b, b.Vehicle));
    }

    public async Task<IEnumerable<BookingResponse>> GetVehicleBookingsAsync(Guid vehicleId)
    {
        var bookings = await _dbContext.Bookings
            .Include(b => b.Vehicle)
            .ThenInclude(v => v!.Category)
            .Where(b => b.VehicleId == vehicleId)
            .OrderByDescending(b => b.StartDateTime)
            .ToListAsync();

        return bookings.Select(b => MapToResponse(b, b.Vehicle));
    }

    public async Task<bool> CheckVehicleAvailabilityAsync(Guid vehicleId, DateTime startDateTime, DateTime endDateTime, Guid? excludeBookingId = null)
    {
        var startUtc = startDateTime.Kind == DateTimeKind.Utc ? startDateTime : DateTime.SpecifyKind(startDateTime, DateTimeKind.Utc);
        var endUtc = endDateTime.Kind == DateTimeKind.Utc ? endDateTime : DateTime.SpecifyKind(endDateTime, DateTimeKind.Utc);

        var query = _dbContext.Bookings
            .Where(b => b.VehicleId == vehicleId && b.Status != BookingStatus.Cancelled);

        if (excludeBookingId.HasValue)
        {
            query = query.Where(b => b.Id != excludeBookingId.Value);
        }

        // Overlap condition: existing Start < new End AND existing End > new Start
        var hasOverlap = await query.AnyAsync(b => b.StartDateTime < endUtc && b.EndDateTime > startUtc);
        return !hasOverlap;
    }

    private static BookingResponse MapToResponse(Booking booking, Vehicle? vehicle)
    {
        VehicleResponse? vehicleDto = null;
        if (vehicle != null)
        {
            vehicleDto = new VehicleResponse
            {
                Id = vehicle.Id,
                Vin = vehicle.Vin,
                LicensePlate = vehicle.LicensePlate,
                Make = vehicle.Make,
                Model = vehicle.Model,
                Year = vehicle.Year,
                DailyRate = vehicle.DailyRate,
                Transmission = vehicle.Transmission,
                FuelType = vehicle.FuelType,
                SeatingCapacity = vehicle.SeatingCapacity,
                HubLocation = vehicle.HubLocation,
                Mileage = vehicle.Mileage,
                Status = vehicle.Status,
                CreatedAt = vehicle.CreatedAt,
                UpdatedAt = vehicle.UpdatedAt,
                VehicleCategoryId = vehicle.VehicleCategoryId,
                CategoryName = vehicle.Category?.Name ?? string.Empty
            };
        }

        return new BookingResponse
        {
            Id = booking.Id,
            CustomerId = booking.CustomerId,
            VehicleId = booking.VehicleId,
            StartDateTime = booking.StartDateTime,
            EndDateTime = booking.EndDateTime,
            Status = booking.Status,
            TotalCost = booking.TotalCost,
            CreatedAt = booking.CreatedAt,
            UpdatedAt = booking.UpdatedAt,
            Vehicle = vehicleDto
        };
    }
}
