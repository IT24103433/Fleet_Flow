using System.Security.Claims;
using FleetFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentalService.Api.Services;
namespace RentalService.Api.Controllers;
[ApiController][Route("api/rentals")][ServiceFilter(typeof(RentalActivationFilter))]
public sealed class RentalsController(PickupCoordinator service):ControllerBase
{
 private Guid UserId=>Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub"),out var id)?id:Guid.Empty;
 [HttpPost("bookings/{bookingId:guid}/pickup")][Authorize(Roles="ADMIN,FLEET_MANAGER")][ProducesResponseType(typeof(RentalResponse),200)][ProducesResponseType(400)][ProducesResponseType(404)][ProducesResponseType(409)][ProducesResponseType(503)]
 public async Task<IActionResult> Pickup(Guid bookingId,PickupRequest request,CancellationToken ct)
 {if(UserId==Guid.Empty)return Unauthorized();var record=await service.StartAsync(bookingId,UserId,request,ct);return record==null?NotFound(new{message="Booking was not found."}):Ok(record);}
 [HttpGet("queue")][Authorize(Roles="ADMIN,FLEET_MANAGER")][ProducesResponseType(typeof(RentalQueueResponse),200)][ProducesResponseType(400)][ProducesResponseType(503)]
 public async Task<IActionResult> Queue(string state="AwaitingPickup",int page=1,int pageSize=25,CancellationToken ct=default)=>Ok(await service.QueueAsync(state,page,pageSize,ct));
 [HttpGet("{id:guid}")][Authorize(Roles="CUSTOMER,ADMIN,FLEET_MANAGER")][ProducesResponseType(typeof(RentalResponse),200)][ProducesResponseType(400)][ProducesResponseType(404)][ProducesResponseType(409)][ProducesResponseType(503)]
 public async Task<IActionResult> Get(Guid id,CancellationToken ct)
 {if(UserId==Guid.Empty)return Unauthorized();var row=await service.GetAsync(id,User.IsInRole("ADMIN")||User.IsInRole("FLEET_MANAGER")?null:UserId,ct);return row==null?NotFound(new{message="Rental was not found."}):Ok(row);}
}
