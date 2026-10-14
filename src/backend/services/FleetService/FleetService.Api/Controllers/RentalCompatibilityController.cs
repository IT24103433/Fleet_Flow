using FleetFlow.Contracts;
using FleetService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace FleetService.Api.Controllers;
[ApiController][Route("api/rentals")]
public sealed class RentalCompatibilityController(RentalForwarder rental):ControllerBase
{
 [HttpGet("queue")][Authorize(Roles="ADMIN,FLEET_MANAGER")]
 public Task<ActionResult> Queue()=>Forward("/queue"+Request.QueryString);
 [HttpGet("{id:guid}")][Authorize(Roles="CUSTOMER,ADMIN,FLEET_MANAGER")]
 public Task<ActionResult> Get(Guid id)=>Forward($"/{id}");
 [HttpPost("bookings/{id:guid}/pickup")][Authorize(Roles="ADMIN,FLEET_MANAGER")]
 public Task<ActionResult> Pickup(Guid id,PickupRequest request)=>Forward($"/bookings/{id}/pickup",request);
 [HttpPost("{id:guid}/complete")][Authorize(Roles="ADMIN,FLEET_MANAGER")]
 public Task<ActionResult> Complete(Guid id,ReturnRequest request)=>Forward($"/{id}/complete",request);
 [HttpGet("/api/bookings/{id:guid}/timeline")][Authorize(Roles="CUSTOMER,ADMIN,FLEET_MANAGER")]
 public Task<ActionResult> Timeline(Guid id)=>rental.Enabled?rental.ForwardAsync(Request,$"/{id}/timeline"):Task.FromResult<ActionResult>(StatusCode(503,new{message="Rental history is unavailable."}));
 private Task<ActionResult> Forward(string path,object? body=null)=>rental.Enabled?rental.ForwardAsync(Request,path,body,"api/rentals"):Task.FromResult<ActionResult>(StatusCode(503,new{message="Rental handovers are unavailable. Please try again."}));
}
