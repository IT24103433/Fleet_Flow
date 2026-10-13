using FleetFlow.Contracts;
using FleetService.Api.Data;
using FleetService.Api.Exceptions;
using FleetService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace FleetService.Api.Controllers;
[ApiController][Route("internal/reservations")][Authorize(Policy=ServiceScopes.ReservationWrite)]
public sealed class ReservationsController(ReservationService service,RentalRouting routing,FleetDbContext db):ControllerBase
{
 [HttpPut("{id:guid}")][ProducesResponseType(typeof(ReservationAcknowledgement),200)][ProducesResponseType(400)][ProducesResponseType(404)][ProducesResponseType(409)][ProducesResponseType(503)]public async Task<IActionResult> Reserve(Guid id,ReserveVehicleCommand command,CancellationToken ct)
 {
  if(!await routing.ReadyAsync(db,ct))return StatusCode(503,new{message="Fleet reservation ownership is not ready."});
  if(id!=command.BookingId)return BadRequest(new{message="Booking identifiers differ."});
  try{return Ok(await service.ReserveAsync(command,ct));}
  catch(ValidationException ex){return BadRequest(new{message=ex.Message});}
  catch(DuplicateException ex){return Conflict(new{message=ex.Message});}
  catch(NotFoundException ex){return NotFound(new{message=ex.Message});}
 }
 [HttpPost("{id:guid}/release")][ProducesResponseType(typeof(ReservationAcknowledgement),200)][ProducesResponseType(404)][ProducesResponseType(503)]public async Task<IActionResult> Release(Guid id,CancellationToken ct)
 {if(!await routing.ReadyAsync(db,ct))return StatusCode(503);var row=await service.ReleaseAsync(id,ct);return row==null?NotFound():Ok(row);}
 [HttpGet("availability")][Authorize(Policy=ServiceScopes.FleetRead)]public async Task<IActionResult> Availability(Guid vehicleId,DateTime startDateTime,DateTime endDateTime,CancellationToken ct)
 {
  if(!await routing.ReadyAsync(db,ct))return StatusCode(503);
  try{return Ok(new{isAvailable=await service.AvailableAsync(vehicleId,startDateTime,endDateTime,ct)});}
  catch(ValidationException ex){return BadRequest(new{isAvailable=false,message=ex.Message});}
  catch(NotFoundException ex){return NotFound(new{isAvailable=false,message=ex.Message});}
 }
}
[ApiController][Route("internal/vehicles")][Authorize(Policy=ServiceScopes.FleetRead)]
public sealed class InternalVehiclesController(IVehicleService service):ControllerBase
{[HttpGet("{id:guid}")]public async Task<IActionResult> Get(Guid id){var item=await service.GetVehicleByIdAsync(id);return item==null?NotFound():Ok(item);}}
