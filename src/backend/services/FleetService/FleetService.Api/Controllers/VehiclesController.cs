using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Exceptions;
using FleetService.Api.Services;

namespace FleetService.Api.Controllers;

[ApiController]
[Route("api/vehicles")]
public class VehiclesController : ControllerBase
{
    private readonly IVehicleService _vehicleService;

    public VehiclesController(IVehicleService vehicleService)
    {
        _vehicleService = vehicleService;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<VehicleResponse>), 200)]
    public async Task<ActionResult<IEnumerable<VehicleResponse>>> GetVehicles(
        [FromQuery] string? category = null,
        [FromQuery] VehicleStatus? status = null,
        [FromQuery] string? fuel = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var vehicles = await _vehicleService.GetVehiclesAsync(category, status, fuel, searchTerm, page, pageSize);
        return Ok(vehicles);
    }

    [HttpGet("{id:guid}", Name = "GetVehicleById")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(VehicleResponse), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<VehicleResponse>> GetById(Guid id)
    {
        var vehicle = await _vehicleService.GetVehicleByIdAsync(id);
        if (vehicle == null)
        {
            return NotFound(new { message = $"Vehicle with ID '{id}' was not found." });
        }

        return Ok(vehicle);
    }

    [HttpPost]
    [Authorize(Roles = "FLEET_MANAGER,ADMIN")]
    [ProducesResponseType(typeof(VehicleResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(409)]
    public async Task<ActionResult<VehicleResponse>> CreateVehicle([FromBody] CreateVehicleRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var created = await _vehicleService.CreateVehicleAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (DuplicateException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
