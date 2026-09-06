using System.ComponentModel.DataAnnotations;
using FleetService.Api.Entities;

namespace FleetService.Api.Dtos;

public class UpdateVehicleStatusRequest
{
    [Required(ErrorMessage = "Status is required.")]
    public VehicleStatus Status { get; set; }
}
