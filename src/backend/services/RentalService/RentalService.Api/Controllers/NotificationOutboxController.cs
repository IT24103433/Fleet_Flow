using System.Security.Claims;
using RentalService.Api.Data;
using RentalService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace RentalService.Api.Controllers;
[ServiceFilter(typeof(RentalActivationFilter))]
[ApiController,Authorize(Roles="ADMIN"),Route("api/notifications/outbox")]
public sealed class NotificationOutboxController(RentalDbContext db):ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery]string? status,CancellationToken ct)
    {
        if(status is not null && status is not ("Pending" or "Retry" or "Failed" or "Delivered"))return BadRequest();
        var query=db.NotificationOutbox.AsNoTracking();if(status is not null)query=query.Where(e=>e.Status==status);
        return Ok(await query.OrderByDescending(e=>e.CreatedAt).Take(100).Select(e=>new
        {e.Id,e.EventType,e.Status,e.Attempts,e.CreatedAt,e.NextAttemptAt,e.InboxDeliveredAt,e.KafkaPublishedAt,e.LastErrorCode,e.ReplayCount}).ToListAsync(ct));
    }
    [HttpPost("{id:guid}/replay")]
    public async Task<IActionResult> Replay(Guid id,CancellationToken ct)
    {
        if(!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub"),out var actor)||actor==Guid.Empty)return Unauthorized();
        var now=DateTime.UtcNow;
        if(db.Database.IsRelational())
        {
            var changed=await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "NotificationOutbox" SET "Status"='Pending',"Attempts"=0,"NextAttemptAt"={now},"LockedUntil"=NULL,"LeaseToken"=NULL,
                    "ReplayCount"="ReplayCount"+1,"LastReplayedByUserId"={actor},"LastReplayedAt"={now}
                WHERE "Id"={id} AND "Status"='Failed' AND ("LockedUntil" IS NULL OR "LockedUntil"<={now})
                """,ct);
            if(changed==1)return Accepted();
        }
        else
        {
            var item=await db.NotificationOutbox.SingleOrDefaultAsync(e=>e.Id==id,ct);
            if(item is not null && item.Status=="Failed" && (item.LockedUntil is null || item.LockedUntil<=now))
            {item.Status="Pending";item.Attempts=0;item.NextAttemptAt=now;item.LockedUntil=null;item.LeaseToken=null;
             item.ReplayCount++;item.LastReplayedByUserId=actor;item.LastReplayedAt=now;await db.SaveChangesAsync(ct);return Accepted();}
        }
        return await db.NotificationOutbox.AnyAsync(e=>e.Id==id,ct)?Conflict(new{message="Only failed, unleased events can be replayed."}):NotFound();
    }
}
