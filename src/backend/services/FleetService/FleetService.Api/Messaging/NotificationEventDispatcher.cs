using System.Net.Http.Headers;
using System.Net.Http.Json;
using FleetFlow.Contracts;
using System.Threading.Channels;
using FleetFlow.ServiceDefaults;
using FleetService.Api.Data;
using FleetService.Api.Entities;
using FleetService.Api.Messaging.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace FleetService.Api.Messaging;
public interface INotificationEventDispatcher { bool TryEnqueue(NotificationDomainEvent domainEvent); }

// The channel is a wake-up hint only. PostgreSQL, not process memory, owns delivery work.
public sealed class NotificationEventDispatcher : BackgroundService,INotificationEventDispatcher
{
    private readonly IServiceScopeFactory scopes;
    private readonly KafkaSettings settings;
    private readonly ILogger<NotificationEventDispatcher> logger;
    private readonly TimeProvider clock;
    private readonly Channel<bool> wake=Channel.CreateBounded<bool>(new BoundedChannelOptions(1){FullMode=BoundedChannelFullMode.DropWrite});
    public NotificationEventDispatcher(IServiceScopeFactory scopes,IKafkaProducerService producer,
        IOptions<KafkaSettings> settings,ILogger<NotificationEventDispatcher> logger,TimeProvider? clock=null)
    { this.scopes=scopes;this.settings=settings.Value;this.logger=logger;this.clock=clock??TimeProvider.System;_ = producer; }
    public bool TryEnqueue(NotificationDomainEvent domainEvent)=>wake.Writer.TryWrite(true);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        while(!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
                using var poll=CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                poll.CancelAfter(TimeSpan.FromSeconds(1));
                try { await wake.Reader.ReadAsync(poll.Token); } catch(OperationCanceledException) when(!stoppingToken.IsCancellationRequested) { }
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { break; }
            catch(Exception ex)
            { logger.LogWarning(ex,"Notification outbox unavailable; durable events remain pending.");
              try { await Task.Delay(1000,stoppingToken); } catch(OperationCanceledException) { break; } }
        }
    }
    public async Task DispatchAsync(NotificationDomainEvent domainEvent,CancellationToken ct=default)
    {
        using(var scope=scopes.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<FleetDbContext>();
            if(!await db.NotificationOutbox.AnyAsync(e=>e.Id==domainEvent.EventId,ct))
            { db.NotificationOutbox.Add(NotificationOutbox.From(domainEvent));await db.SaveChangesAsync(ct); }
        }
        await DispatchBatchAsync(ct);
    }
    public async Task<int> DispatchBatchAsync(CancellationToken ct=default)
    {
        using var scope=scopes.CreateScope();var provider=scope.ServiceProvider;
        var db=provider.GetRequiredService<FleetDbContext>();
        var now=clock.GetUtcNow().UtcDateTime;var until=now.AddSeconds(60);var lease=Guid.NewGuid();
        List<NotificationOutbox> rows;
        if(db.Database.IsRelational())
            rows=await db.NotificationOutbox.FromSqlInterpolated($"""
                UPDATE "NotificationOutbox" SET "LeaseToken"={lease},"LockedUntil"={until},"Attempts"="Attempts"+1
                WHERE "Id" IN (SELECT "Id" FROM "NotificationOutbox"
                    WHERE "Status" IN ('Pending','Retry') AND "NextAttemptAt"<={now}
                    AND ("LockedUntil" IS NULL OR "LockedUntil"<={now}) ORDER BY "CreatedAt" LIMIT 1 FOR UPDATE SKIP LOCKED)
                RETURNING *
                """).AsNoTracking().ToListAsync(ct);
        else
        {
            rows=await db.NotificationOutbox.Where(e=>(e.Status=="Pending" || e.Status=="Retry") && e.NextAttemptAt<=now &&
                (e.LockedUntil==null || e.LockedUntil<=now)).Take(1).ToListAsync(ct);
            foreach(var row in rows){row.LeaseToken=lease;row.LockedUntil=until;row.Attempts++;}
            await db.SaveChangesAsync(ct);
        }
        foreach(var row in rows)
        {
            try
            {
                var domainEvent=NotificationEventProcessor.Deserialize(row.Payload);
                if(row.InboxDeliveredAt is null)
                {
                    var routing=provider.GetService<NotificationRouting>();
                    if(routing?.Remote==true)
                    {
                        var token=await provider.GetRequiredService<IServiceTokenProvider>().GetTokenAsync(ct);
                        using var request=new HttpRequestMessage(HttpMethod.Post,"internal/notifications/events")
                        { Content=new StringContent(row.Payload,System.Text.Encoding.UTF8,"application/json") };
                        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
                        using var response=await provider.GetRequiredService<IHttpClientFactory>().CreateClient("NotificationService").SendAsync(request,ct);
                        response.EnsureSuccessStatusCode();
                        var acknowledgement=await response.Content.ReadFromJsonAsync<NotificationDeliveryAcknowledgement>(ct);
                        if(acknowledgement?.EventId!=row.Id)throw new HttpRequestException("Notification service did not acknowledge the durable event.");
                    }
                    else await provider.GetRequiredService<NotificationEventProcessor>().ProcessAsync(domainEvent,ct);
                    row.InboxDeliveredAt=clock.GetUtcNow().UtcDateTime;
                    if(!await SaveAsync(db,row,ct)) continue;
                }
                if(settings.Enabled && row.KafkaPublishedAt is null)
                {
                    var topic=domainEvent is BookingCreatedEvent or BookingCancelledEvent?settings.BookingEventsTopic:settings.MaintenanceEventsTopic;
                    await provider.GetRequiredService<IReliableNotificationPublisher>().PublishAsync(topic,row.Id,row.Payload,ct);
                    row.KafkaPublishedAt=clock.GetUtcNow().UtcDateTime;
                }
                row.DeliveredAt=clock.GetUtcNow().UtcDateTime;row.Status="Delivered";row.LastErrorCode=null;
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested) { throw; }
            catch(Exception ex)
            {
                row.LastErrorCode=ex is ArgumentException or System.Text.Json.JsonException?"InvalidEvent":"DeliveryUnavailable";
                row.Status=row.LastErrorCode=="InvalidEvent" || row.Attempts>=8?"Failed":"Retry";
                row.NextAttemptAt=clock.GetUtcNow().UtcDateTime.AddSeconds(Math.Min(300,Math.Pow(2,Math.Min(8,row.Attempts))));
                logger.LogWarning(ex,"Outbox event {EventId} retained as {Status} after attempt {Attempt}.",row.Id,row.Status,row.Attempts);
            }
            // Keep the token in the predicate; an expired worker must not overwrite a new lease.
            row.LockedUntil=null;
            await SaveAsync(db,row,ct);
        }
        return rows.Count;
    }
    private static async Task<bool> SaveAsync(FleetDbContext db,NotificationOutbox row,CancellationToken ct)
    {
        if(db.Database.IsRelational())
            return await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "NotificationOutbox" SET "InboxDeliveredAt"={row.InboxDeliveredAt},"KafkaPublishedAt"={row.KafkaPublishedAt},
                    "Status"={row.Status},"DeliveredAt"={row.DeliveredAt},"LastErrorCode"={row.LastErrorCode},
                    "NextAttemptAt"={row.NextAttemptAt},"LockedUntil"={row.LockedUntil}
                WHERE "Id"={row.Id} AND "LeaseToken"={row.LeaseToken}
                """,ct)==1;
        await db.SaveChangesAsync(ct);return true;
    }
}
