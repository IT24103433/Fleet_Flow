using System.Net;
using System.Net.Http.Json;
using FleetFlow.Contracts;
using FleetFlow.ServiceDefaults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RentalService.Api.Data;
using RentalService.Api.Messaging;
using RentalService.Api.Services;
namespace RentalService.Tests;
public sealed class RentalOutboxTests
{
 [RentalPostgresFact]public async Task RentalOutboxRetainsInboxCheckpointAcrossKafkaFailureAndRestart()
 {
  await using var f=await RentalPostgresFixture.Create();
  await using(var db=f.Rental())await new BookingCoordinator(db,new BookingRecoveryTests.Gateway(f)).CreateAsync(f.Customer,BookingRecoveryTests.Request(f),Guid.NewGuid());
  var clock=new Clock();var inbox=new InboxHandler();var kafka=new Broker();Guid id;
  using(var provider=Provider(f,inbox,kafka,clock)){
   var worker=Worker(provider,clock);Assert.Equal(1,await worker.DispatchBatchAsync());
   await using var db=f.Rental();var row=await db.NotificationOutbox.SingleAsync();id=row.Id;Assert.Equal("Retry",row.Status);Assert.NotNull(row.InboxDeliveredAt);Assert.Null(row.KafkaPublishedAt);Assert.Null(row.LockedUntil);
  }
  clock.Now=clock.Now.AddSeconds(5);
  using(var restarted=Provider(f,inbox,kafka,clock))Assert.Equal(1,await Worker(restarted,clock).DispatchBatchAsync());
  await using(var db=f.Rental()){var row=await db.NotificationOutbox.SingleAsync();Assert.Equal(id,row.Id);Assert.Equal("Delivered",row.Status);Assert.NotNull(row.KafkaPublishedAt);Assert.Equal(2,row.Attempts);}
  Assert.Equal(1,inbox.Calls);Assert.Equal(2,kafka.Calls);
 }
 [Fact]public async Task RentalDeliveryReplayIsAdminOnlyAndCannotReplayAnAlreadyQueuedEvent()
 {
  using var f=new RentalFactory();using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<RentalDbContext>();var id=Guid.NewGuid();
  db.NotificationOutbox.Add(new(){Id=id,EventType="BookingCreatedEvent",Payload="private payload",Status="Failed",CreatedAt=DateTime.UtcNow,NextAttemptAt=DateTime.UtcNow});await db.SaveChangesAsync();
  using var customer=f.Browser(Guid.NewGuid());Assert.Equal(HttpStatusCode.Forbidden,(await customer.GetAsync("/api/notifications/outbox")).StatusCode);
  using var admin=f.Browser(Guid.NewGuid(),"ADMIN");var response=await admin.GetAsync("/api/notifications/outbox");Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.DoesNotContain("private payload",await response.Content.ReadAsStringAsync());
  Assert.Equal(HttpStatusCode.Accepted,(await admin.PostAsync($"/api/notifications/outbox/{id}/replay",null)).StatusCode);Assert.Equal(HttpStatusCode.Conflict,(await admin.PostAsync($"/api/notifications/outbox/{id}/replay",null)).StatusCode);
 }
 private static ServiceProvider Provider(RentalPostgresFixture f,InboxHandler inbox,Broker kafka,TimeProvider clock)
 {
  var services=new ServiceCollection();services.AddDbContext<RentalDbContext>(o=>o.UseNpgsql(f.Target));services.AddSingleton<IServiceTokenProvider>(new Token());
  services.AddSingleton<IHttpClientFactory>(new ClientFactory(inbox));services.AddSingleton<IReliableNotificationPublisher>(kafka);services.AddSingleton(clock);return services.BuildServiceProvider();
 }
 private static NotificationEventDispatcher Worker(ServiceProvider provider,TimeProvider clock)=>new(provider.GetRequiredService<IServiceScopeFactory>(),Options.Create(new KafkaSettings{Enabled=true}),NullLogger<NotificationEventDispatcher>.Instance,clock);
 private sealed class Clock:TimeProvider{public DateTimeOffset Now=DateTimeOffset.UtcNow.AddSeconds(1);public override DateTimeOffset GetUtcNow()=>Now;}
 private sealed class Token:IServiceTokenProvider{public Task<string> GetTokenAsync(CancellationToken ct)=>Task.FromResult("restricted-service-token");}
 private sealed class Broker:IReliableNotificationPublisher{public int Calls;public Task PublishAsync(string topic,Guid id,string payload,CancellationToken ct){if(++Calls==1)throw new HttpRequestException("Kafka outage");return Task.CompletedTask;}}
 private sealed class ClientFactory(InboxHandler handler):IHttpClientFactory{public HttpClient CreateClient(string name)=>new(handler,false){BaseAddress=new Uri("http://127.0.0.1:5004/")};}
 private sealed class InboxHandler:HttpMessageHandler
 {
  public int Calls;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
  {
   Calls++;Assert.Equal("restricted-service-token",request.Headers.Authorization?.Parameter);
   var item=NotificationEventRendering.Deserialize(await request.Content!.ReadAsStringAsync(ct));return new(HttpStatusCode.OK){Content=JsonContent.Create(new NotificationDeliveryAcknowledgement(item.EventId,true))};
  }
 }
}
