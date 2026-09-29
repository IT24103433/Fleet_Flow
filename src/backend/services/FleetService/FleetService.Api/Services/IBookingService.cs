using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FleetService.Api.Dtos;

namespace FleetService.Api.Services;

public interface IBookingService
{
    Task<BookingResponse> CreateBookingAsync(Guid customerId, CreateBookingRequest request);
    Task<BookingResponse?> GetBookingByIdAsync(Guid id);
    Task<IEnumerable<BookingResponse>> GetCustomerBookingsAsync(Guid customerId);
    Task<BookingResponse?> CancelBookingAsync(Guid id, Guid customerId);
    Task<bool> CheckVehicleAvailabilityAsync(Guid vehicleId, DateTime startDateTime, DateTime endDateTime, Guid? excludeBookingId = null);
}
