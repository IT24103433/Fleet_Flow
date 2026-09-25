using System;
using System.ComponentModel.DataAnnotations;
using FleetService.Api.Entities;

namespace FleetService.Api.Dtos;

public class CreateBookingRequest
{
    [Required(ErrorMessage = "Vehicle ID is required.")]
    public Guid VehicleId { get; set; }

    [Required(ErrorMessage = "Start date and time is required.")]
    public DateTime StartDateTime { get; set; }

    [Required(ErrorMessage = "End date and time is required.")]
    public DateTime EndDateTime { get; set; }

    public BookingStatus? Status { get; set; }
}
