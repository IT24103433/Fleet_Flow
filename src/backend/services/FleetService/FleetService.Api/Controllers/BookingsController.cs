using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FleetService.Api.Dtos;
using FleetService.Api.Exceptions;
using FleetService.Api.Services;

namespace FleetService.Api.Controllers;

[ApiController]
[Route("api/bookings")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingsController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    [HttpPost]
    [Authorize(Roles = "CUSTOMER")]
    [ProducesResponseType(typeof(BookingResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<ActionResult<BookingResponse>> CreateBooking([FromBody] CreateBookingRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var customerId = GetCurrentUserId();
        if (customerId == Guid.Empty)
        {
            return Unauthorized(new { message = "User identity claim missing or invalid." });
        }

        try
        {
            var created = await _bookingService.CreateBookingAsync(customerId, request);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (DuplicateException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}", Name = "GetBookingById")]
    [Authorize(Roles = "CUSTOMER,FLEET_MANAGER,ADMIN")]
    [ProducesResponseType(typeof(BookingResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<BookingResponse>> GetById(Guid id)
    {
        var requesterId = GetCurrentUserId();
        if (requesterId == Guid.Empty)
        {
            return Unauthorized(new { message = "User identity claim missing or invalid." });
        }

        var booking = await _bookingService.GetBookingByIdAsync(id);
        if (booking == null)
        {
            return NotFound(new { message = $"Booking with ID '{id}' was not found." });
        }

        var hasStaffAccess = User.IsInRole("FLEET_MANAGER") || User.IsInRole("ADMIN");
        if (!hasStaffAccess && booking.CustomerId != requesterId)
        {
            return NotFound(new { message = $"Booking with ID '{id}' was not found." });
        }

        return Ok(booking);
    }

    [HttpGet]
    [Authorize(Roles = "CUSTOMER")]
    [ProducesResponseType(typeof(IEnumerable<BookingResponse>), 200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<IEnumerable<BookingResponse>>> GetMyBookings()
    {
        var customerId = GetCurrentUserId();
        if (customerId == Guid.Empty)
        {
            return Unauthorized(new { message = "User identity claim missing or invalid." });
        }

        var bookings = await _bookingService.GetCustomerBookingsAsync(customerId);
        return Ok(bookings);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "CUSTOMER")]
    [ProducesResponseType(typeof(BookingResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<ActionResult<BookingResponse>> CancelBooking(Guid id)
    {
        var customerId = GetCurrentUserId();
        if (customerId == Guid.Empty)
        {
            return Unauthorized(new { message = "User identity claim missing or invalid." });
        }

        try
        {
            var cancelled = await _bookingService.CancelBookingAsync(id, customerId);
            if (cancelled == null)
            {
                return NotFound(new { message = $"Booking with ID '{id}' was not found." });
            }

            return Ok(cancelled);
        }
        catch (DuplicateException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("check-availability")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CheckAvailability([FromQuery] Guid vehicleId, [FromQuery] DateTime startDateTime, [FromQuery] DateTime endDateTime)
    {
        if (vehicleId == Guid.Empty || startDateTime == default || endDateTime == default)
        {
            return BadRequest(new { message = "vehicleId, startDateTime, and endDateTime parameters are required." });
        }

        try
        {
            var isAvailable = await _bookingService.CheckVehicleAvailabilityAsync(vehicleId, startDateTime, endDateTime);
            if (!isAvailable)
            {
                return Conflict(new
                {
                    isAvailable = false,
                    message = "The selected vehicle has an overlapping booking for the requested timeframe.",
                    vehicleId,
                    startDateTime,
                    endDateTime
                });
            }

            return Ok(new { isAvailable = true, vehicleId, startDateTime, endDateTime });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { isAvailable = false, message = ex.Message });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { isAvailable = false, message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub")
            ?? User.FindFirst("id")
            ?? User.FindFirst(ClaimTypes.Name);

        if (claim != null && Guid.TryParse(claim.Value, out var userId))
        {
            return userId;
        }

        return Guid.Empty;
    }
}
