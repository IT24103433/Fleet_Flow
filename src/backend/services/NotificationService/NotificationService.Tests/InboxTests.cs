using FleetService.Api.Messaging.Events;
using Microsoft.EntityFrameworkCore;
using NotificationService.Api.Data;
using NotificationService.Api.Services;
namespace NotificationService.Tests;
public sealed class InboxTests
{
    internal static NotificationDbContext Database(string? name=null)=>new(new DbContextOptionsBuilder<NotificationDbContext>().UseInMemoryDatabase(name??Guid.NewGuid().ToString()).Options);
    internal static BookingCreatedEvent Event(Guid? owner=null)=>new(){EventId=Guid.NewGuid(),OccurredAt=DateTime.UtcNow,CustomerId=owner??Guid.NewGuid(),BookingId=Guid.NewGuid(),VehicleId=Guid.NewGuid()};
    [Fact]public async Task DuplicateEventAfterRestartPreservesReadStateAndId()
    {
        var name=Guid.NewGuid().ToString();var item=Event();Guid id;
        await using(var db=Database(name))
        {
            Assert.True(await new NotificationEventProcessor(db).ProcessAsync(item));
            id=Assert.Single(db.Notifications).Id;await new InboxService(db).ReadAsync(item.CustomerId,id,default);
        }
        await using(var db=Database(name))
        {
            Assert.False(await new NotificationEventProcessor(db).ProcessAsync(item));
            var inbox=Assert.Single(await new InboxService(db).GetInboxAsync(item.CustomerId,default));
            Assert.Equal(id,inbox.Id);Assert.True(inbox.IsRead);
            Assert.Empty(await new InboxService(db).GetInboxAsync(Guid.NewGuid(),default));
            Assert.False(await new InboxService(db).ReadAsync(Guid.NewGuid(),id,default));
        }
    }
    [Fact]public async Task ReusedEventIdWithDifferentPayloadIsRejected()
    {
        await using var db=Database();var item=Event();var processor=new NotificationEventProcessor(db);
        await processor.ProcessAsync(item);
        await Assert.ThrowsAsync<NotificationEventConflictException>(()=>processor.ProcessAsync(item with{CustomerId=Guid.NewGuid()}));
        Assert.Single(db.Notifications);
    }
    [Fact]public async Task InvalidRecipientsDoNotProducePartialData()
    {
        await using var db=Database();await Assert.ThrowsAsync<ArgumentException>(()=>new NotificationEventProcessor(db).ProcessAsync(new MaintenanceScheduledEvent
        {EventId=Guid.NewGuid(),OccurredAt=DateTime.UtcNow,MaintenanceId=Guid.NewGuid(),VehicleId=Guid.NewGuid(),Activity="Service",TargetUserIds=[Guid.NewGuid(),Guid.Empty]}));
        Assert.Empty(db.Notifications);Assert.Empty(db.Receipts);
    }
    [Fact]public async Task HistoricalReplayRetainsMigratedFields()
    {
        await using var db=Database();var item=Event();var id=Guid.NewGuid();var created=DateTime.UtcNow.AddDays(-1);
        db.Notifications.Add(new(){Id=id,EventId=item.EventId,TargetUserId=item.CustomerId,Category="Booking",Title="Booking created",
            Message=$"Booking {item.BookingId} for vehicle {item.VehicleId} was created successfully.",RelatedEntityId=item.BookingId,VehicleId=item.VehicleId,CreatedAt=created,IsRead=true});
        await db.SaveChangesAsync();await new NotificationEventProcessor(db).ProcessAsync(item);
        var row=Assert.Single(db.Notifications);Assert.Equal(id,row.Id);Assert.Equal(created,row.CreatedAt);Assert.True(row.IsRead);
    }
    [Fact]public async Task RejectedKafkaOffsetIsStoredOnce()
    {
        await using var db=Database();var processor=new NotificationEventProcessor(db);
        await processor.RejectAsync("topic",0,1,"invalid",default);await processor.RejectAsync("topic",0,1,"invalid",default);
        Assert.Single(db.RejectedEvents);
    }
}
