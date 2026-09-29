using System.Security.Claims;
using FleetService.Api.Dtos;
using FleetService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetService.Api.Controllers;

[ApiController, Authorize, Route("api/notifications")]
public class NotificationsController(NotificationService service) : ControllerBase
{
    private Guid? CurrentUserId => User.Identity?.IsAuthenticated == true &&
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) &&
        id != Guid.Empty ? id : null;

    [HttpGet]
    public async Task<IActionResult> GetInbox(CancellationToken ct = default) => CurrentUserId is Guid id
        ? Ok(await service.GetInboxAsync(id, ct)) : Unauthorized();

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct = default) => CurrentUserId is Guid id
        ? Ok(new { count = await service.GetUnreadCountAsync(id, ct) }) : Unauthorized();

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct = default)
    {
        if (CurrentUserId is not Guid userId) return Unauthorized();
        return await service.MarkReadAsync(userId, id, ct) ? NoContent() : NotFound();
    }

    [HttpPost("system"), Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> CreateSystemNotification(CreateSystemNotificationRequest request, CancellationToken ct = default)
    {
        if (CurrentUserId == null) return Unauthorized();
        if (!User.IsInRole("ADMIN")) return Forbid();
        if (!ModelState.IsValid || request.TargetUserId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Message) ||
            request.Title.Length > 200 || request.Message.Length > 2000) return BadRequest();
        await service.PersistAsync(Guid.NewGuid(), request.TargetUserId, "System", request.Title.Trim(), request.Message.Trim(), ct: ct);
        return StatusCode(StatusCodes.Status201Created);
    }
}
