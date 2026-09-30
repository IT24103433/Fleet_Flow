using System.Text.Json;
using FleetService.Api.Data;
using FleetService.Api.Entities;
using FleetService.Api.Messaging;
using FleetService.Api.Messaging.Events;
using FleetService.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FleetService.Tests;

public class NotificationTests
{
    private static FleetDbContext Database() => new(new DbContextOptionsBuilder<FleetDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static BookingCreatedEvent BookingEvent(Guid userId) => new()
    {
        EventId = Guid.NewGuid(), OccurredAt = DateTime.UtcNow,
        BookingId = Guid.NewGuid(), VehicleId = Guid.NewGuid(), CustomerId = userId
    };

    [Fact]
    public async Task PersistedNotification_SurvivesNewContext_WithUnreadStateAndUtcTimestamp()
    {
        var options = new DbContextOptionsBuilder<FleetDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var db = new FleetDbContext(options);
        var userId = Guid.NewGuid();
        var before = DateTime.UtcNow;
        await new NotificationService(db).PersistAsync(Guid.NewGuid(), userId, "System", "Update", "Account update");
        using var fresh = new FleetDbContext(options);
        var item = Assert.Single(await new NotificationService(fresh).GetInboxAsync(userId));
        Assert.Equal("System", item.Category);
        Assert.Equal("Update", item.Title);
        Assert.Equal("Account update", item.Message);
        Assert.False(item.IsRead);
        Assert.InRange(item.CreatedAt, before, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, item.CreatedAt.Kind);
    }

    [Fact]
    public async Task Inbox_IsolatedAndEmptyForAnotherUser()
    {
        using var db = Database();
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var service = new NotificationService(db);
        await service.PersistAsync(Guid.NewGuid(), owner, "System", "Private", "Message");
        Assert.Single(await service.GetInboxAsync(owner));
        Assert.Empty(await service.GetInboxAsync(other));
        Assert.Equal(0, await service.GetUnreadCountAsync(other));
    }

    [Fact]
    public async Task MarkRead_RequiresOwnership_IsIdempotentAndUpdatesCount()
    {
        using var db = Database();
        var owner = Guid.NewGuid();
        var service = new NotificationService(db);
        await service.PersistAsync(Guid.NewGuid(), owner, "System", "Private", "Message");
        var id = Assert.Single(await service.GetInboxAsync(owner)).Id;
        Assert.False(await service.MarkReadAsync(Guid.NewGuid(), id));
        Assert.Equal(1, await service.GetUnreadCountAsync(owner));
        Assert.True(await service.MarkReadAsync(owner, id));
        Assert.True(await service.MarkReadAsync(owner, id));
        Assert.Equal(0, await service.GetUnreadCountAsync(owner));
        Assert.True(Assert.Single(await service.GetInboxAsync(owner)).IsRead);
        Assert.False(await service.MarkReadAsync(owner, Guid.NewGuid()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BookingContracts_RoundTripAndPersistOnlyForCustomer(bool cancelled)
    {
        using var db = Database();
        var owner = Guid.NewGuid();
        var created = BookingEvent(owner);
        NotificationDomainEvent domainEvent = cancelled ? new BookingCancelledEvent
        {
            EventId = created.EventId, OccurredAt = created.OccurredAt,
            BookingId = created.BookingId, VehicleId = created.VehicleId, CustomerId = owner
        } : created;
        var payload = JsonSerializer.Serialize<object>(domainEvent, NotificationEventProcessor.JsonOptions);
        var replay = NotificationEventProcessor.Deserialize(payload);
        var processor = new NotificationEventProcessor(new NotificationService(db));
        await processor.ProcessAsync(replay);
        await processor.ProcessAsync(replay);
        var item = Assert.Single(await new NotificationService(db).GetInboxAsync(owner));
        Assert.Equal(created.BookingId, item.RelatedEntityId);
        Assert.Equal(created.VehicleId, item.VehicleId);
        Assert.Contains(created.BookingId.ToString(), item.Message);
        Assert.Equal(cancelled ? "Booking cancelled" : "Booking created", item.Title);
        Assert.Empty(await new NotificationService(db).GetInboxAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MaintenanceContracts_RoundTripAndDeduplicateEachRecipient(bool statusChanged)
    {
        using var db = Database();
        var owner = Guid.NewGuid();
        var second = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var workId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        NotificationDomainEvent domainEvent = statusChanged ? new MaintenanceStatusChangedEvent
        {
            EventId = eventId, OccurredAt = DateTime.UtcNow, MaintenanceId = workId, VehicleId = vehicleId,
            Activity = "Oil change", Status = "Completed", TargetUserIds = [owner, owner, second]
        } : new MaintenanceScheduledEvent
        {
            EventId = eventId, OccurredAt = DateTime.UtcNow, MaintenanceId = workId, VehicleId = vehicleId,
            Activity = "Oil change", TargetUserIds = [owner, owner, second]
        };
        var replay = NotificationEventProcessor.Deserialize(JsonSerializer.Serialize<object>(domainEvent, NotificationEventProcessor.JsonOptions));
        var service = new NotificationService(db);
        var processor = new NotificationEventProcessor(service);
        await processor.ProcessAsync(replay);
        await processor.ProcessAsync(replay);
        Assert.Equal(2, await db.Notifications.CountAsync());
        var item = Assert.Single(await service.GetInboxAsync(owner));
        Assert.Single(await service.GetInboxAsync(second));
        Assert.Equal(workId, item.RelatedEntityId);
        Assert.Equal(vehicleId, item.VehicleId);
        Assert.Contains("Oil change", item.Message);
        Assert.Contains(vehicleId.ToString(), item.Message);
        if (statusChanged) Assert.Contains("Completed", item.Message);
        Assert.Empty(await service.GetInboxAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DuplicateEvent_DoesNotResetReadState()
    {
        using var db = Database();
        var owner = Guid.NewGuid();
        var domainEvent = BookingEvent(owner);
        var service = new NotificationService(db);
        var processor = new NotificationEventProcessor(service);
        await processor.ProcessAsync(domainEvent);
        await service.MarkReadAsync(owner, Assert.Single(await service.GetInboxAsync(owner)).Id);
        await processor.ProcessAsync(domainEvent);
        Assert.True(Assert.Single(await service.GetInboxAsync(owner)).IsRead);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"eventType\":\"Unknown\"}")]
    [InlineData("invalid-json")]
    public void InvalidPayload_IsRejected(string payload) => Assert.ThrowsAny<Exception>(() => NotificationEventProcessor.Deserialize(payload));

    [Fact]
    public async Task InvalidEventIdsAndRecipients_DoNotPersistPartialNotifications()
    {
        using var db = Database();
        var processor = new NotificationEventProcessor(new NotificationService(db));
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync(BookingEvent(Guid.NewGuid()) with { EventId = Guid.Empty }));
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync(BookingEvent(Guid.NewGuid()) with { OccurredAt = default }));
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync(BookingEvent(Guid.Empty)));
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessAsync(new MaintenanceScheduledEvent
        {
            EventId = Guid.NewGuid(), OccurredAt = DateTime.UtcNow, MaintenanceId = Guid.NewGuid(), VehicleId = Guid.NewGuid(),
            Activity = "Oil change", TargetUserIds = [Guid.NewGuid(), Guid.Empty]
        }));
        Assert.Empty(db.Notifications);
    }

    [Fact]
    public void Model_HasUniqueEventAndRecipientIndex()
    {
        using var db = Database();
        var entity = db.Model.FindEntityType(typeof(Notification))!;
        Assert.Contains(entity.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "EventId", "TargetUserId" }));
    }

    private sealed class ThrowingProducer : IKafkaProducerService
    {
        public Task PublishAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Kafka unavailable");
    }

    [Fact]
    public async Task Dispatcher_KafkaFailureStillPersistsLocalNotification()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<FleetDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddScoped<NotificationService>();
        services.AddScoped<NotificationEventProcessor>();
        using var provider = services.BuildServiceProvider();
        using var dispatcher = new NotificationEventDispatcher(provider.GetRequiredService<IServiceScopeFactory>(),
            new ThrowingProducer(), Options.Create(new KafkaSettings()), NullLogger<NotificationEventDispatcher>.Instance);
        var domainEvent = BookingEvent(Guid.NewGuid());
        await dispatcher.DispatchAsync(domainEvent);
        using var scope = provider.CreateScope();
        Assert.Single(await scope.ServiceProvider.GetRequiredService<NotificationService>().GetInboxAsync(domainEvent.CustomerId));
    }
}
