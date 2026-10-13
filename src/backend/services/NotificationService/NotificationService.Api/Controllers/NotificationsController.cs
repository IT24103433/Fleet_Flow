using System.Security.Claims;
using System.Text.Json;
using FleetFlow.Contracts;
using FleetService.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Api.Data;
using NotificationService.Api.Services;
namespace NotificationService.Api.Controllers;
[ApiController,Authorize,Route("api/notifications"),ServiceFilter(typeof(NotificationActivationFilter))]
public sealed class NotificationsController(InboxService inbox,NotificationDbContext db):ControllerBase
{
    private Guid? UserId=>Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub"),out var id)&&id!=Guid.Empty?id:null;
    [HttpGet,ProducesResponseType(typeof(NotificationResponse[]),200)]public async Task<IActionResult> GetInbox(CancellationToken ct)=>UserId is Guid id?Ok(await inbox.GetInboxAsync(id,ct)):Unauthorized();
    [HttpGet("unread-count")]public async Task<IActionResult> Unread(CancellationToken ct)=>UserId is Guid id?Ok(new{count=await inbox.CountAsync(id,ct)}):Unauthorized();
    [HttpPatch("{id:guid}/read"),ProducesResponseType(204)]public async Task<IActionResult> Read(Guid id,CancellationToken ct)=>UserId is Guid user?
        await inbox.ReadAsync(user,id,ct)?NoContent():NotFound():Unauthorized();
    [HttpPost("system"),Authorize(Roles="ADMIN"),ProducesResponseType(201)]
    public async Task<IActionResult> System(CreateSystemNotificationRequest request,CancellationToken ct)
    {
        if(UserId is null)return Unauthorized();
        if(request.TargetUserId==Guid.Empty || string.IsNullOrWhiteSpace(request.Title)||string.IsNullOrWhiteSpace(request.Message))return BadRequest();
        await inbox.CreateSystemAsync(request,ct);return StatusCode(201);
    }
    [HttpGet("delivery-failures"),Authorize(Roles="ADMIN")]
    public async Task<IActionResult> Failures(CancellationToken ct)=>Ok(await db.RejectedEvents.AsNoTracking()
        .OrderByDescending(e=>e.CreatedAt).Take(100).Select(e=>new{e.Id,e.Topic,e.Partition,e.Offset,e.ErrorCode,e.CreatedAt}).ToListAsync(ct));
}
[ApiController,Route("internal/notifications/events"),Authorize(Policy=ServiceScopes.NotificationDeliver),ServiceFilter(typeof(NotificationActivationFilter))]
public sealed class NotificationEventsController(NotificationEventProcessor processor):ControllerBase
{
    [HttpPost,RequestSizeLimit(65536),ProducesResponseType(typeof(NotificationDeliveryAcknowledgement),200),ProducesResponseType(400),ProducesResponseType(409)]
    public async Task<IActionResult> Receive(JsonElement payload,CancellationToken ct)
    {
        try
        {var item=NotificationEventRendering.Deserialize(payload.GetRawText());return Ok(new NotificationDeliveryAcknowledgement(item.EventId,await processor.ProcessAsync(item,ct)));}
        catch(Exception ex)when(ex is ArgumentException or JsonException){return BadRequest(new{message="Invalid notification event."});}
        catch(NotificationEventConflictException){return Conflict(new{message="Event identifier already has different content."});}
    }
}
