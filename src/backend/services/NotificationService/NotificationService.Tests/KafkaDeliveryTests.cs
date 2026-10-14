using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using FleetFlow.Contracts;
using FleetFlow.ServiceDefaults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NotificationMigration;
using Npgsql;
using NotificationService.Api.Data;
using NotificationService.Api.Messaging;
using NotificationService.Api.Services;
namespace NotificationService.Tests;
public sealed class NotificationKafkaFactAttribute:FactAttribute
{
    public NotificationKafkaFactAttribute(){if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLEET_TEST_KAFKA"))||string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLEET_TEST_POSTGRES")))Skip="Requires explicit local isolated PostgreSQL and Kafka test endpoints.";}
}
public sealed class KafkaDeliveryTests
{
    [NotificationKafkaFact]public async Task RealKafkaDuplicateReplayAndRejectedPayloadRemainDurable()
    {
        var broker=Environment.GetEnvironmentVariable("FLEET_TEST_KAFKA");
        if(broker is not ("127.0.0.1:9092" or "localhost:9092"))throw new InvalidOperationException("Kafka tests require the explicit loopback broker.");
        var topic="fleetflow_hardening_notifications_"+Guid.NewGuid().ToString("N")[..12];
        using var admin=new AdminClientBuilder(new AdminClientConfig{BootstrapServers=broker,SocketTimeoutMs=5000}).Build();
        await admin.CreateTopicsAsync([new TopicSpecification{Name=topic,NumPartitions=1,ReplicationFactor=1},new TopicSpecification{Name=topic+"_maintenance",NumPartitions=1,ReplicationFactor=1}]);
        try
        {
            await PostgresNotificationTests.WithDatabase(async connection=>
            {
                var sql=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"migrations/notifications/002_notifications.sql"));
                var receipt=await NotificationDataMigration.ImportAsync(connection,"[]",NotificationDataMigration.Hash("[]"),sql,true);
                var services=new ServiceCollection();services.AddDbContext<NotificationDbContext>(o=>o.UseNpgsql(connection));services.AddScoped<NotificationEventProcessor>();
                services.AddSingleton(new NotificationActivation(true,receipt.MigrationId));
                var descriptor=ServiceDatabaseTool.FoundationMigration.Descriptor("notifications") with{Database=new NpgsqlConnectionStringBuilder(connection).Database!};
                services.AddSingleton<IDatabaseReadiness>(new NotificationReadiness(new(connection,descriptor),new(true,receipt.MigrationId)));
                using var provider=services.BuildServiceProvider();
                var settings=Options.Create(new KafkaSettings{Enabled=true,BootstrapServers=broker!,BookingEventsTopic=topic,MaintenanceEventsTopic=topic+"_maintenance",NotificationConsumerGroupId=topic+"_group"});
                var item=InboxTests.Event();var payload=JsonSerializer.Serialize(item,item.GetType(),NotificationEventRendering.JsonOptions);
                using var producer=new ProducerBuilder<string,string>(new ProducerConfig{BootstrapServers=broker,Acks=Acks.All,EnableIdempotence=true,MessageTimeoutMs=5000}).Build();
                using(var consumer=new NotificationKafkaConsumer(provider.GetRequiredService<IServiceScopeFactory>(),settings,NullLogger<NotificationKafkaConsumer>.Instance))
                {
                    await consumer.StartAsync(default);
                    await producer.ProduceAsync(topic,new(){Key=item.EventId.ToString(),Value=payload});
                    await producer.ProduceAsync(topic,new(){Key=item.EventId.ToString(),Value=payload});
                    await WaitFor(connection,async db=>await db.Notifications.CountAsync()==1&&await db.Receipts.CountAsync()==1);
                    await using(var db=Context(connection)){var row=await db.Notifications.SingleAsync();await new InboxService(db).ReadAsync(item.CustomerId,row.Id,default);}
                    await consumer.StopAsync(default);
                }
                using(var restarted=new NotificationKafkaConsumer(provider.GetRequiredService<IServiceScopeFactory>(),settings,NullLogger<NotificationKafkaConsumer>.Instance))
                {
                    await restarted.StartAsync(default);
                    await producer.ProduceAsync(topic,new(){Key=item.EventId.ToString(),Value=payload});
                    await producer.ProduceAsync(topic,new(){Key="invalid",Value="not-json"});
                    await WaitFor(connection,async db=>await db.RejectedEvents.CountAsync()==1);
                    await using var check=Context(connection);Assert.True((await check.Notifications.SingleAsync()).IsRead);Assert.Equal(1,await check.Receipts.CountAsync());
                    await restarted.StopAsync(default);
                }
            });
        }
        finally
        {
            if(!System.Text.RegularExpressions.Regex.IsMatch(topic,"^fleetflow_hardening_notifications_[a-f0-9]{12}$"))throw new InvalidOperationException("Unsafe topic cleanup target.");
            await admin.DeleteTopicsAsync([topic,topic+"_maintenance"]);
            // Both names derive from the one validated, test-owned prefix.
        }
    }
    private static NotificationDbContext Context(string connection)=>new(new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(connection).Options);
    private static async Task WaitFor(string connection,Func<NotificationDbContext,Task<bool>> predicate)
    {
        var deadline=DateTime.UtcNow.AddSeconds(20);
        while(DateTime.UtcNow<deadline){await using var db=Context(connection);if(await predicate(db))return;await Task.Delay(100);}
        throw new Xunit.Sdk.XunitException("Kafka test delivery did not complete before the isolated deadline.");
    }
}
