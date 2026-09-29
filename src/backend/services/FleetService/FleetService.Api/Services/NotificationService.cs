using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FleetService.Api.Services;

public class NotificationService(FleetDbContext db)
{
    public async Task<IReadOnlyList<NotificationResponse>> GetInboxAsync(Guid userId, CancellationToken ct = default) =>
        await db.Notifications.AsNoTracking().Where(n => n.TargetUserId == userId)
            .OrderByDescending(n => n.CreatedAt).ThenBy(n => n.Id)
            .Select(n => new NotificationResponse(n.Id, n.Category, n.Title, n.Message,
                n.RelatedEntityId, n.VehicleId, n.CreatedAt, n.IsRead)).ToListAsync(ct);

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default) =>
        db.Notifications.CountAsync(n => n.TargetUserId == userId && !n.IsRead, ct);

    public async Task<bool> MarkReadAsync(Guid userId, Guid id, CancellationToken ct = default)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(n => n.Id == id && n.TargetUserId == userId, ct);
        if (notification == null) return false;
        notification.IsRead = true;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task PersistAsync(Guid eventId, Guid userId, string category, string title, string message,
        Guid? relatedId = null, Guid? vehicleId = null, CancellationToken ct = default)
    {
        if (eventId == Guid.Empty || userId == Guid.Empty || string.IsNullOrWhiteSpace(title) ||
            title.Length > 200 || string.IsNullOrWhiteSpace(message) || message.Length > 2000)
            throw new ArgumentException("A valid event, target user, title and message are required.");
        if (await db.Notifications.AnyAsync(n => n.EventId == eventId && n.TargetUserId == userId, ct)) return;
        var notification = new Notification
        {
            EventId = eventId, TargetUserId = userId, Category = category, Title = title,
            Message = message, RelatedEntityId = relatedId, VehicleId = vehicleId
        };
        db.Notifications.Add(notification);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Notifications_EventId_TargetUserId" })
        {
            // Local dispatch and Kafka replay may race; the database is the final deduplication boundary.
            db.Entry(notification).State = EntityState.Detached;
        }
    }
}
