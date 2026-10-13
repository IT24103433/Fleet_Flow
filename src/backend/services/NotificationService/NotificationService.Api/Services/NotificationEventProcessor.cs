using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FleetFlow.Contracts;
using FleetService.Api.Messaging.Events;
using Microsoft.EntityFrameworkCore;
using NotificationService.Api.Data;
namespace NotificationService.Api.Services;
public sealed class NotificationEventConflictException:Exception;
public sealed class NotificationEventProcessor(NotificationDbContext db)
{
    public async Task<bool> ProcessAsync(NotificationDomainEvent domainEvent,CancellationToken ct=default)
    {
        var items=NotificationEventRendering.Render(domainEvent);
        var payload=JsonSerializer.Serialize(domainEvent,domainEvent.GetType(),NotificationEventRendering.JsonOptions);
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        await using var transaction=db.Database.IsRelational()?await db.Database.BeginTransactionAsync(ct):null;
        if(db.Database.IsRelational())
        {
            var inserted=await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "NotificationReceipts" ("EventId","PayloadHash","ProcessedAt") VALUES ({domainEvent.EventId},{hash},{DateTime.UtcNow})
                ON CONFLICT ("EventId") DO NOTHING
                """,ct);
            if(inserted==0)
            {
                var prior=await db.Receipts.AsNoTracking().SingleAsync(r=>r.EventId==domainEvent.EventId,ct);
                if(prior.PayloadHash!=hash)throw new NotificationEventConflictException();
                return false;
            }
        }
        else
        {
            var prior=await db.Receipts.FindAsync([domainEvent.EventId],ct);
            if(prior is not null){if(prior.PayloadHash!=hash)throw new NotificationEventConflictException();return false;}
            db.Receipts.Add(new(){EventId=domainEvent.EventId,PayloadHash=hash,ProcessedAt=DateTime.UtcNow});
        }
        // Migrated historical events have no original payload. Compare rendered content,
        // retain their original IDs/timestamps/read states, and fill only missing recipients.
        var existing=await db.Notifications.AsNoTracking().Where(n=>n.EventId==domainEvent.EventId).ToListAsync(ct);
        foreach(var row in existing)
            if(!items.Any(n=>n.TargetUserId==row.TargetUserId&&n.Category==row.Category&&n.Title==row.Title&&n.Message==row.Message&&
                n.RelatedEntityId==row.RelatedEntityId&&n.VehicleId==row.VehicleId))throw new NotificationEventConflictException();
        foreach(var item in items)
        {
            if(existing.Any(n=>n.TargetUserId==item.TargetUserId))continue;
            var id=Guid.NewGuid();
            if(db.Database.IsRelational())
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Notifications" ("Id","EventId","TargetUserId","Category","Title","Message","RelatedEntityId","VehicleId","CreatedAt","IsRead")
                    VALUES ({id},{item.EventId},{item.TargetUserId},{item.Category},{item.Title},{item.Message},{item.RelatedEntityId},{item.VehicleId},{item.CreatedAt},false)
                    ON CONFLICT ("EventId","TargetUserId") DO NOTHING
                    """,ct);
            else db.Notifications.Add(new(){Id=id,EventId=item.EventId,TargetUserId=item.TargetUserId,Category=item.Category,
                Title=item.Title,Message=item.Message,RelatedEntityId=item.RelatedEntityId,VehicleId=item.VehicleId,CreatedAt=item.CreatedAt});
        }
        if(transaction is not null)await transaction.CommitAsync(ct);else await db.SaveChangesAsync(ct);
        return true;
    }
    public async Task RejectAsync(string topic,int partition,long offset,string payload,CancellationToken ct)
    {
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        if(db.Database.IsRelational())
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "RejectedNotificationEvents" ("Id","Topic","Partition","Offset","PayloadHash","Payload","ErrorCode","CreatedAt")
                VALUES ({Guid.NewGuid()},{topic},{partition},{offset},{hash},{payload[..Math.Min(16384,payload.Length)]},'InvalidEvent',{DateTime.UtcNow})
                ON CONFLICT ("Topic","Partition","Offset") DO NOTHING
                """,ct);
        else
        {
            if(await db.RejectedEvents.AnyAsync(e=>e.Topic==topic&&e.Partition==partition&&e.Offset==offset,ct))return;
            db.RejectedEvents.Add(new(){Topic=topic,Partition=partition,Offset=offset,PayloadHash=hash,Payload=payload[..Math.Min(16384,payload.Length)]});
            await db.SaveChangesAsync(ct);
        }
    }
}
