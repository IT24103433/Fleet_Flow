using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FleetService.Api.Dtos;
using FleetService.Api.Services;

namespace FleetService.Api.Controllers;

[ApiController]
[Route("api/vehicle-categories")]
public class VehicleCategoriesController : ControllerBase
{
    private readonly IVehicleService _vehicleService;

    public VehicleCategoriesController(IVehicleService vehicleService)
    {
        _vehicleService = vehicleService;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<VehicleCategoryResponse>), 200)]
    public async Task<ActionResult<IEnumerable<VehicleCategoryResponse>>> GetCategories()
    {
        var categories = await _vehicleService.GetCategoriesAsync();
        return Ok(categories);
    }
}
