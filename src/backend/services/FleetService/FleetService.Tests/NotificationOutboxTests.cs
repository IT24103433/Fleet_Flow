using System.Net;
using FleetFlow.ServiceDefaults;
using FleetService.Api.Data;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using FleetService.Api.Messaging;
using FleetService.Api.Messaging.Events;
using FleetService.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
namespace FleetService.Tests;
public sealed class NotificationOutboxTests
{
    [Fact]public async Task BookingAndCancellationCommitStableEventsWithoutDispatcher()
    {
        await using var db=new FleetDbContext(new DbContextOptionsBuilder<FleetDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var category=new VehicleCategory{Id=Guid.NewGuid(),Name="Test"};var vehicle=new Vehicle{VehicleCategoryId=category.Id,DailyRate=100};db.AddRange(category,vehicle);await db.SaveChangesAsync();
        var owner=Guid.NewGuid();var booking=await new BookingService(db).CreateBookingAsync(owner,new(){VehicleId=vehicle.Id,StartDateTime=DateTime.UtcNow.AddDays(1),EndDateTime=DateTime.UtcNow.AddDays(2)});
        var created=Assert.Single(db.NotificationOutbox);Assert.Equal("BookingCreatedEvent",created.EventType);
        Assert.Equal(owner,Assert.IsType<BookingCreatedEvent>(NotificationEventProcessor.Deserialize(created.Payload)).CustomerId);
        await new BookingService(db).CancelBookingAsync(booking.Id,owner);Assert.Equal(2,await db.NotificationOutbox.CountAsync());
        Assert.Equal(2,db.NotificationOutbox.Select(e=>e.Id).Distinct().Count());
    }
    [Fact]public async Task InvalidBusinessChangeNeverCreatesOutboxEvent()
    {
        await using var db=new FleetDbContext(new DbContextOptionsBuilder<FleetDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        await Assert.ThrowsAnyAsync<Exception>(()=>new BookingService(db).CreateBookingAsync(Guid.Empty,new()));Assert.Empty(db.NotificationOutbox);
    }
    [Fact]public async Task RestartsRetainRetryWorkAndFailedDeliveryRemainsInspectable()
    {
        var services=new ServiceCollection();var name=Guid.NewGuid().ToString();var clock=new Clock();var handler=new RejectHandler();
        services.AddDbContext<FleetDbContext>(o=>o.UseInMemoryDatabase(name));services.AddSingleton(new NotificationRouting(true,new Uri("http://localhost:5004/")));
        services.AddSingleton<IServiceTokenProvider>(new Token());services.AddSingleton<IHttpClientFactory>(new Clients(handler));
        using var provider=services.BuildServiceProvider();Guid id;
        using(var scope=provider.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<FleetDbContext>();var item=new BookingCreatedEvent{EventId=Guid.NewGuid(),OccurredAt=DateTime.UtcNow,BookingId=Guid.NewGuid(),VehicleId=Guid.NewGuid(),CustomerId=Guid.NewGuid()};
            var row=NotificationOutbox.From(item);id=row.Id;db.NotificationOutbox.Add(row);await db.SaveChangesAsync();
        }
        for(var attempt=1;attempt<=8;attempt++)
        {
            using var worker=new NotificationEventDispatcher(provider.GetRequiredService<IServiceScopeFactory>(),new Producer(),Options.Create(new KafkaSettings()),NullLogger<NotificationEventDispatcher>.Instance,clock);
            await worker.DispatchBatchAsync();clock.Advance(TimeSpan.FromMinutes(6));
        }
        using(var scope=provider.CreateScope())
        {
            var row=await scope.ServiceProvider.GetRequiredService<FleetDbContext>().NotificationOutbox.FindAsync(id);
            Assert.Equal("Failed",row!.Status);Assert.Equal(8,row.Attempts);Assert.Null(row.DeliveredAt);Assert.Equal("DeliveryUnavailable",row.LastErrorCode);
        }
        Assert.Equal(8,handler.Calls);
    }
    [Fact]public async Task KafkaOutageRetainsOutboxAndRetryDoesNotResetInboxReadState()
    {
        var services=new ServiceCollection();var name=Guid.NewGuid().ToString();var clock=new Clock();var publisher=new FaultPublisher();
        services.AddDbContext<FleetDbContext>(o=>o.UseInMemoryDatabase(name));services.AddScoped<NotificationService>();services.AddScoped<NotificationEventProcessor>();
        services.AddSingleton<IReliableNotificationPublisher>(publisher);
        using var provider=services.BuildServiceProvider();var owner=Guid.NewGuid();Guid id;
        using(var scope=provider.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<FleetDbContext>();var row=NotificationOutbox.From(new BookingCreatedEvent
            {EventId=Guid.NewGuid(),OccurredAt=DateTime.UtcNow,BookingId=Guid.NewGuid(),VehicleId=Guid.NewGuid(),CustomerId=owner});
            id=row.Id;db.NotificationOutbox.Add(row);await db.SaveChangesAsync();
        }
        using(var worker=new NotificationEventDispatcher(provider.GetRequiredService<IServiceScopeFactory>(),new Producer(),Options.Create(new KafkaSettings{Enabled=true}),NullLogger<NotificationEventDispatcher>.Instance,clock))
            await worker.DispatchBatchAsync();
        using(var scope=provider.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<FleetDbContext>();var row=await db.NotificationOutbox.FindAsync(id);
            Assert.Equal("Retry",row!.Status);Assert.NotNull(row.InboxDeliveredAt);Assert.Null(row.KafkaPublishedAt);
            var notification=Assert.Single(db.Notifications);await scope.ServiceProvider.GetRequiredService<NotificationService>().MarkReadAsync(owner,notification.Id);
        }
        publisher.Fail=false;clock.Advance(TimeSpan.FromSeconds(3));
        using(var restarted=new NotificationEventDispatcher(provider.GetRequiredService<IServiceScopeFactory>(),new Producer(),Options.Create(new KafkaSettings{Enabled=true}),NullLogger<NotificationEventDispatcher>.Instance,clock))
            await restarted.DispatchBatchAsync();
        using(var scope=provider.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<FleetDbContext>();Assert.True(Assert.Single(db.Notifications).IsRead);
            var row=await db.NotificationOutbox.FindAsync(id);Assert.Equal("Delivered",row!.Status);Assert.NotNull(row.KafkaPublishedAt);
        }
    }
    private sealed class FaultPublisher:IReliableNotificationPublisher
    {public bool Fail=true;public Task PublishAsync(string topic,Guid id,string payload,CancellationToken ct)=>Fail?Task.FromException(new HttpRequestException("Kafka unavailable")):Task.CompletedTask;}
    private sealed class Clock:TimeProvider{private DateTimeOffset now=DateTimeOffset.UtcNow.AddSeconds(1);public override DateTimeOffset GetUtcNow()=>now;public void Advance(TimeSpan time)=>now+=time;}
    private sealed class RejectHandler:HttpMessageHandler{public int Calls;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Calls++;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));}}
    private sealed class Clients(HttpMessageHandler handler):IHttpClientFactory{public HttpClient CreateClient(string name)=>new(handler,false){BaseAddress=new Uri("http://localhost:5004/")};}
    private sealed class Token:IServiceTokenProvider{public Task<string> GetTokenAsync(CancellationToken ct)=>Task.FromResult("service-token");}
    private sealed class Producer:IKafkaProducerService{public Task PublishAsync<T>(string topic,string key,T message,CancellationToken cancellationToken=default)=>Task.CompletedTask;}
}
