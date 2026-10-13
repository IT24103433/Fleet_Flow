using FleetService.Api.Dtos;
using Microsoft.EntityFrameworkCore;
using NotificationService.Api.Data;
namespace NotificationService.Api.Services;
public sealed class InboxService(NotificationDbContext db)
{
    public async Task<IReadOnlyList<NotificationResponse>> GetInboxAsync(Guid userId,CancellationToken ct)=>
        await db.Notifications.AsNoTracking().Where(n=>n.TargetUserId==userId).OrderByDescending(n=>n.CreatedAt).ThenBy(n=>n.Id)
            .Select(n=>new NotificationResponse(n.Id,n.Category,n.Title,n.Message,n.RelatedEntityId,n.VehicleId,n.CreatedAt,n.IsRead)).ToListAsync(ct);
    public Task<int> CountAsync(Guid userId,CancellationToken ct)=>db.Notifications.CountAsync(n=>n.TargetUserId==userId&&!n.IsRead,ct);
    public async Task<bool> ReadAsync(Guid userId,Guid id,CancellationToken ct)
    {
        var item=await db.Notifications.SingleOrDefaultAsync(n=>n.Id==id&&n.TargetUserId==userId,ct);
        if(item is null)return false;item.IsRead=true;await db.SaveChangesAsync(ct);return true;
    }
    public async Task CreateSystemAsync(CreateSystemNotificationRequest request,CancellationToken ct)
    {
        db.Notifications.Add(new(){EventId=Guid.NewGuid(),TargetUserId=request.TargetUserId,Title=request.Title.Trim(),Message=request.Message.Trim()});
        await db.SaveChangesAsync(ct);
    }
}
