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

namespace FleetService.Api.Services;

public class BookingService : IBookingService
{
    private readonly FleetDbContext _dbContext;
    private readonly INotificationEventDispatcher? _notifications;
    private readonly ILogger<BookingService>? _logger;

    public BookingService(FleetDbContext dbContext, INotificationEventDispatcher? notifications = null, ILogger<BookingService>? logger = null)
    {
        _dbContext = dbContext;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<BookingResponse> CreateBookingAsync(Guid customerId, CreateBookingRequest request)
    {
        if (customerId == Guid.Empty)
        {
            throw new ValidationException("Customer ID is required.");
        }

        if (request == null)
        {
            throw new ValidationException("Booking request is required.");
        }

        var startUtc = NormalizeUtc(request.StartDateTime);
        var endUtc = NormalizeUtc(request.EndDateTime);
        ValidateRentalPeriod(startUtc, endUtc);

        await using var transaction = await VehicleWriteTransaction.BeginAsync(_dbContext, request.VehicleId);
        var vehicle = await GetBookableVehicleAsync(request.VehicleId);

        if (await HasActiveOverlapAsync(request.VehicleId, startUtc, endUtc))
        {
            throw new DuplicateException("The selected vehicle has an overlapping booking for the requested timeframe.");
        }

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
            Status = BookingStatus.Confirmed,
            TotalCost = totalCost,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Bookings.Add(booking);
        await _dbContext.SaveChangesAsync();
        if (transaction != null) await transaction.CommitAsync();

        try
        {
            _notifications?.TryEnqueue(new BookingCreatedEvent
            {
                EventId = Guid.NewGuid(), OccurredAt = booking.CreatedAt,
                BookingId = booking.Id, CustomerId = customerId, VehicleId = booking.VehicleId
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Booking {BookingId} persisted but notification dispatch failed.", booking.Id);
        }

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
            .OrderByDescending(b => b.StartDateTime)
            .ThenByDescending(b => b.CreatedAt)
            .ToListAsync();

        return bookings.Select(b => MapToResponse(b, b.Vehicle));
    }

    public async Task<BookingResponse?> CancelBookingAsync(Guid id, Guid customerId)
    {
        if (id == Guid.Empty || customerId == Guid.Empty)
        {
            throw new ValidationException("Booking and customer identifiers are required.");
        }

        var vehicleId = await _dbContext.Bookings.AsNoTracking()
            .Where(b => b.Id == id && b.CustomerId == customerId)
            .Select(b => (Guid?)b.VehicleId).SingleOrDefaultAsync();
        if (vehicleId == null) return null;

        await using var transaction = await VehicleWriteTransaction.BeginAsync(_dbContext, vehicleId.Value);
        var booking = await _dbContext.Bookings
            .Include(b => b.Vehicle)
            .ThenInclude(v => v!.Category)
            .FirstOrDefaultAsync(b => b.Id == id && b.CustomerId == customerId);

        if (booking == null)
        {
            return null;
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            throw new DuplicateException("This booking has already been cancelled.");
        }

        if (booking.Status != BookingStatus.Pending && booking.Status != BookingStatus.Confirmed)
        {
            throw new ValidationException($"A booking in '{booking.Status}' status cannot be cancelled.");
        }

        booking.Status = BookingStatus.Cancelled;
        booking.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        if (transaction != null) await transaction.CommitAsync();

        try
        {
            _notifications?.TryEnqueue(new BookingCancelledEvent
            {
                EventId = Guid.NewGuid(), OccurredAt = booking.UpdatedAt.Value,
                BookingId = booking.Id, CustomerId = booking.CustomerId, VehicleId = booking.VehicleId
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Booking {BookingId} was cancelled but notification dispatch failed.", booking.Id);
        }

        return MapToResponse(booking, booking.Vehicle);
    }

    public async Task<bool> CheckVehicleAvailabilityAsync(Guid vehicleId, DateTime startDateTime, DateTime endDateTime, Guid? excludeBookingId = null)
    {
        var startUtc = NormalizeUtc(startDateTime);
        var endUtc = NormalizeUtc(endDateTime);
        ValidateRentalPeriod(startUtc, endUtc);
        await GetBookableVehicleAsync(vehicleId);

        return !await HasActiveOverlapAsync(vehicleId, startUtc, endUtc, excludeBookingId);
    }

    private async Task<Vehicle> GetBookableVehicleAsync(Guid vehicleId)
    {
        if (vehicleId == Guid.Empty)
        {
            throw new ValidationException("Vehicle ID is required.");
        }

        var vehicle = await _dbContext.Vehicles
            .Include(v => v.Category)
            .FirstOrDefaultAsync(v => v.Id == vehicleId);

        if (vehicle == null)
        {
            throw new NotFoundException($"Vehicle with ID '{vehicleId}' was not found.");
        }

        if (vehicle.Status != VehicleStatus.Available)
        {
            throw new ValidationException($"Vehicle is currently in '{vehicle.Status}' status and is not available for rental booking.");
        }

        return vehicle;
    }

    private async Task<bool> HasActiveOverlapAsync(
        Guid vehicleId,
        DateTime startUtc,
        DateTime endUtc,
        Guid? excludeBookingId = null)
    {
        var query = _dbContext.Bookings.Where(b =>
            b.VehicleId == vehicleId &&
            (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));

        if (excludeBookingId.HasValue)
        {
            query = query.Where(b => b.Id != excludeBookingId.Value);
        }

        return await query.AnyAsync(b => b.StartDateTime < endUtc && b.EndDateTime > startUtc);
    }

    private static void ValidateRentalPeriod(DateTime startUtc, DateTime endUtc)
    {
        if (startUtc == default || endUtc == default)
        {
            throw new ValidationException("Start date and time and end date and time are required.");
        }

        if (startUtc < DateTime.UtcNow.AddMinutes(-5))
        {
            throw new ValidationException("Start date and time must be in the future.");
        }

        if (endUtc <= startUtc)
        {
            throw new ValidationException("End date and time must be after start date and time.");
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
