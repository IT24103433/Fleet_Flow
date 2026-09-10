using FleetService.Api.Dtos;
using FleetService.Api.Exceptions;
using FleetService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetService.Api.Controllers;

[ApiController]
[Route("api/vehicles/{vehicleId:guid}/images")]
public class VehicleImagesController : ControllerBase
{
    private readonly IVehicleImageService _vehicleImageService;

    public VehicleImagesController(IVehicleImageService vehicleImageService)
    {
        _vehicleImageService = vehicleImageService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<VehicleImageResponse>>> GetImages(Guid vehicleId)
    {
        try
        {
            var images = await _vehicleImageService.GetImagesAsync(vehicleId);
            return Ok(images);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN,FLEET_MANAGER")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<VehicleImageResponse>> UploadImage(Guid vehicleId, [FromForm] UploadVehicleImageRequest request)
    {
        if (request?.File == null)
        {
            return BadRequest(new { message = "An image file must be provided." });
        }

        try
        {
            var response = await _vehicleImageService.UploadImageAsync(vehicleId, request.File, request.Caption);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{imageId:guid}")]
    [Authorize(Roles = "ADMIN,FLEET_MANAGER")]
    public async Task<IActionResult> DeleteImage(Guid vehicleId, Guid imageId)
    {
        try
        {
            await _vehicleImageService.DeleteImageAsync(vehicleId, imageId);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
