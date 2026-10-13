using System.Security.Claims;
using FleetFlow.Contracts;
using FleetService.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentalService.Api.Services;
namespace RentalService.Api.Controllers;
[ApiController][Route("api/bookings")][ServiceFilter(typeof(RentalActivationFilter))]
public sealed class BookingsController(BookingCoordinator service,IFleetGateway fleet):ControllerBase
{
 private Guid UserId=>Guid.TryParse((User.FindFirst(ClaimTypes.NameIdentifier)??User.FindFirst("sub")??User.FindFirst("id")??User.FindFirst(ClaimTypes.Name))?.Value,out var id)?id:Guid.Empty;
 [HttpPost][Authorize(Roles="CUSTOMER")][ProducesResponseType(typeof(BookingResponse),201)][ProducesResponseType(400)][ProducesResponseType(404)][ProducesResponseType(409)][ProducesResponseType(503)]
 public async Task<IActionResult> Create(CreateBookingRequest request,CancellationToken ct)
 {
  if(UserId==Guid.Empty)return Unauthorized(new{message="User identity claim missing or invalid."});
  Guid key=Guid.Empty;
  if(Request.Headers.TryGetValue("Idempotency-Key",out var raw)&&(!Guid.TryParse(raw,out key)||key==Guid.Empty))return BadRequest(new{message="Idempotency-Key must be a non-empty UUID."});
  try{var item=await service.CreateAsync(UserId,request,key,ct);return CreatedAtAction(nameof(Get),new{id=item.Id},item);}
  catch(BookingRequestException ex){return StatusCode(ex.Status,new{message=ex.Message});}
 }
 [HttpGet("{id:guid}")][Authorize(Roles="CUSTOMER,FLEET_MANAGER,ADMIN")][ProducesResponseType(typeof(BookingResponse),200)][ProducesResponseType(404)][ProducesResponseType(503)]
 public async Task<IActionResult> Get(Guid id,CancellationToken ct)
 {
  if(UserId==Guid.Empty)return Unauthorized();var row=await service.GetAsync(id,ct,User.IsInRole("ADMIN")||User.IsInRole("FLEET_MANAGER")?null:UserId);
  return row==null||!(User.IsInRole("ADMIN")||User.IsInRole("FLEET_MANAGER"))&&row.CustomerId!=UserId?NotFound(new{message=$"Booking with ID '{id}' was not found."}):Ok(row);
 }
 [HttpGet][Authorize(Roles="CUSTOMER")][ProducesResponseType(typeof(BookingResponse[]),200)][ProducesResponseType(503)]
 public async Task<IActionResult> Mine(CancellationToken ct)=>UserId==Guid.Empty?Unauthorized():Ok(await service.MineAsync(UserId,ct));
 [HttpPost("{id:guid}/cancel")][Authorize(Roles="CUSTOMER")][ProducesResponseType(typeof(BookingResponse),200)][ProducesResponseType(400)][ProducesResponseType(404)][ProducesResponseType(409)][ProducesResponseType(503)]
 public async Task<IActionResult> Cancel(Guid id,CancellationToken ct)
 {
  if(UserId==Guid.Empty)return Unauthorized();
  try{var row=await service.CancelAsync(id,UserId,ct);return row==null?NotFound(new{message=$"Booking with ID '{id}' was not found."}):Ok(row);}
  catch(BookingRequestException ex){return StatusCode(ex.Status,new{message=ex.Message});}
 }
 [HttpGet("check-availability")][AllowAnonymous]
 public async Task<IActionResult> Availability(Guid vehicleId,DateTime startDateTime,DateTime endDateTime,CancellationToken ct)
 {
  if(vehicleId==Guid.Empty||startDateTime==default||endDateTime==default)return BadRequest(new{message="vehicleId, startDateTime, and endDateTime parameters are required."});
  startDateTime=RentalPeriod.Utc(startDateTime);endDateTime=RentalPeriod.Utc(endDateTime);
  if(RentalPeriod.Error(startDateTime,endDateTime) is {} error)return BadRequest(new{isAvailable=false,message=error});
  try{
   var available=await fleet.AvailableAsync(vehicleId,startDateTime,endDateTime,ct);
   return available?Ok(new{isAvailable=true,vehicleId,startDateTime,endDateTime}):Conflict(new{isAvailable=false,message="The selected vehicle has an overlapping booking for the requested timeframe.",vehicleId,startDateTime,endDateTime});
  }catch(BookingRequestException ex){return StatusCode(ex.Status,new{isAvailable=false,message=ex.Message});}
 }
}
