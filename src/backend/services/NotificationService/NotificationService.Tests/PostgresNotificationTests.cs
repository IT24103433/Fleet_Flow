using Microsoft.EntityFrameworkCore;
using Npgsql;
using NotificationMigration;
using NotificationService.Api.Data;
using NotificationService.Api.Services;
using ServiceDatabaseTool;
namespace NotificationService.Tests;
public sealed class NotificationPostgresFactAttribute:FactAttribute
{ public NotificationPostgresFactAttribute(){if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLEET_TEST_POSTGRES")))Skip="Requires an isolated loopback fleetflow_hardening_* database.";} }
public sealed class PostgresNotificationTests
{
    [Theory][InlineData("Host=example.com;Database=fleetflow_notifications",false)][InlineData("Host=localhost;Database=fleetflow_auth",true)]
    public void RemoteAndUnrelatedMigrationTargetsAreRefused(string connection,bool legacy)=>Assert.Throws<InvalidOperationException>(()=>NotificationDataMigration.Guard(connection,legacy));
    [Fact]public void TamperedExportIsRefused()=>Assert.Throws<InvalidOperationException>(()=>NotificationDataMigration.Validate("[]",new string('0',64)));
    [NotificationPostgresFact]public async Task MigrationPreservesAllFieldsIsIdempotentAndGatesReadiness()
    {
        await WithDatabase(async connection=>
        {
            var item=new InboxNotification{Id=Guid.NewGuid(),EventId=Guid.NewGuid(),TargetUserId=Guid.NewGuid(),Title="Migrated",Message="History",IsRead=true,
                CreatedAt=new DateTime(2026,10,1,12,0,0,DateTimeKind.Utc),VehicleId=Guid.NewGuid(),RelatedEntityId=Guid.NewGuid()};
            var json=NotificationDataMigration.Canonical([item]);var hash=NotificationDataMigration.Hash(json);var sql=await Sql();
            var dry=await NotificationDataMigration.ImportAsync(connection,json,hash,sql,false);Assert.False(dry.Applied);
            var receipt=await NotificationDataMigration.ImportAsync(connection,json,hash,sql,true);Assert.True(receipt.Applied);
            Assert.Equal(json,await NotificationDataMigration.ExportAsync(connection,false));
            var repeated=await NotificationDataMigration.ImportAsync(connection,json,hash,sql,true);Assert.Equal(receipt.MigrationId,repeated.MigrationId);Assert.False(repeated.Applied);
            var descriptor=FoundationMigration.Descriptor("notifications") with{Database=new NpgsqlConnectionStringBuilder(connection).Database!};
            Assert.True(await new NotificationReadiness(new(connection,descriptor),new(true,receipt.MigrationId)).IsReadyAsync(default));
            Assert.False(await new NotificationReadiness(new(connection,descriptor),new(true,Guid.NewGuid())).IsReadyAsync(default));
            await using var db=Context(connection);db.Notifications.Add(new(){EventId=Guid.NewGuid(),TargetUserId=Guid.NewGuid(),Title="Later",Message="Business write"});await db.SaveChangesAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(()=>NotificationDataMigration.ImportAsync(connection,json,hash,sql,true));
            Assert.Equal(2,await db.Notifications.CountAsync());
        });
    }
    [NotificationPostgresFact]public async Task ConcurrentInternalAndKafkaDeliveryIsDeduplicatedTransactionally()
    {
        await WithDatabase(async connection=>
        {
            await Initialize(connection);var item=InboxTests.Event();
            await using var first=Context(connection);await using var second=Context(connection);
            var results=await Task.WhenAll(new NotificationEventProcessor(first).ProcessAsync(item),new NotificationEventProcessor(second).ProcessAsync(item));
            Assert.Single(results,value=>value);await using var check=Context(connection);
            Assert.Equal(1,await check.Notifications.CountAsync());Assert.Equal(1,await check.Receipts.CountAsync());
            var id=(await check.Notifications.SingleAsync()).Id;await new InboxService(check).ReadAsync(item.CustomerId,id,default);
            await using var restarted=Context(connection);Assert.False(await new NotificationEventProcessor(restarted).ProcessAsync(item));
            Assert.True((await restarted.Notifications.SingleAsync()).IsRead);
        });
    }
    [NotificationPostgresFact]public async Task DatabaseFailureRollsBackEveryRecipientAndReceipt()
    {
        await WithDatabase(async connection=>
        {
            await Initialize(connection);var failUser=Guid.Parse("11111111-1111-4111-8111-111111111111");
            await using var db=Context(connection);
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Notifications\" ADD CONSTRAINT test_recipient_failure CHECK (\"TargetUserId\" <> '11111111-1111-4111-8111-111111111111')");
            var item=new FleetService.Api.Messaging.Events.MaintenanceScheduledEvent{EventId=Guid.NewGuid(),OccurredAt=DateTime.UtcNow,
                MaintenanceId=Guid.NewGuid(),VehicleId=Guid.NewGuid(),Activity="Service",TargetUserIds=[Guid.NewGuid(),failUser]};
            await Assert.ThrowsAnyAsync<Exception>(()=>new NotificationEventProcessor(db).ProcessAsync(item));
            await using var fresh=Context(connection);Assert.Empty(await fresh.Notifications.ToListAsync());Assert.Empty(await fresh.Receipts.ToListAsync());
        });
    }
    [NotificationPostgresFact]public async Task RollbackReconciliationKeepsHistoryAndAcknowledgedReadStates()
    {
        var settings=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FLEET_TEST_POSTGRES"));
        if(settings.Host is not ("localhost" or "127.0.0.1")||settings.Database?.StartsWith("fleetflow_hardening_",StringComparison.Ordinal)!=true)throw new InvalidOperationException("Unsafe test database.");
        settings.Database="postgres";var name="fleetflow_hardening_notification_rollback_"+Guid.NewGuid().ToString("N")[..12];
        await using(var admin=new NpgsqlConnection(settings.ConnectionString))
        {await admin.OpenAsync();await using var create=new NpgsqlCommand($"CREATE DATABASE \"{name}\"",admin);await create.ExecuteNonQueryAsync();}
        var owned=new NpgsqlConnectionStringBuilder(settings.ConnectionString){Database=name};
        try
        {
            var old=new InboxNotification{Id=Guid.NewGuid(),EventId=Guid.NewGuid(),TargetUserId=Guid.NewGuid(),Title="Existing",Message="History",CreatedAt=new DateTime(2026,10,1,0,0,0,DateTimeKind.Utc),IsRead=true};
            await using(var db=Context(owned.ConnectionString)){await db.Database.EnsureCreatedAsync();db.Notifications.Add(old);await db.SaveChangesAsync();}
            var exported=new InboxNotification{Id=old.Id,EventId=old.EventId,TargetUserId=old.TargetUserId,Category=old.Category,Title=old.Title,Message=old.Message,CreatedAt=old.CreatedAt,IsRead=false};
            var added=new InboxNotification{Id=Guid.NewGuid(),EventId=Guid.NewGuid(),TargetUserId=old.TargetUserId,Title="New service",Message="Later history",CreatedAt=old.CreatedAt.AddHours(1),IsRead=true};
            var json=NotificationDataMigration.Canonical([exported,added]);
            Assert.Equal(2,await NotificationDataMigration.ReconcileLegacyAsync(owned.ConnectionString,json,NotificationDataMigration.Hash(json)));
            await using(var db=Context(owned.ConnectionString)){Assert.Equal(2,await db.Notifications.CountAsync());Assert.True((await db.Notifications.FindAsync(old.Id))!.IsRead);}
            exported.Title="Tampered";var changed=NotificationDataMigration.Canonical([exported]);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>NotificationDataMigration.ReconcileLegacyAsync(owned.ConnectionString,changed,NotificationDataMigration.Hash(changed)));
            await using(var db=Context(owned.ConnectionString))Assert.Equal("Existing",(await db.Notifications.FindAsync(old.Id))!.Title);
        }
        finally
        {
            if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^fleetflow_hardening_notification_rollback_[a-f0-9]{12}$"))throw new InvalidOperationException("Unsafe cleanup target.");
            using(var pool=new NpgsqlConnection(owned.ConnectionString))NpgsqlConnection.ClearPool(pool);
            await using var admin=new NpgsqlConnection(settings.ConnectionString);await admin.OpenAsync();await using var drop=new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();
        }
    }
    private static NotificationDbContext Context(string connection)=>new(new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(connection).Options);
    private static Task<string> Sql()=>File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"migrations/notifications/002_notifications.sql"));
    private static async Task Initialize(string connection)
    {var json="[]";await NotificationDataMigration.ImportAsync(connection,json,NotificationDataMigration.Hash(json),await Sql(),true);}
    internal static async Task WithDatabase(Func<string,Task> action)
    {
        var settings=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FLEET_TEST_POSTGRES"));
        if(settings.Host is not ("localhost" or "127.0.0.1") || settings.Database?.StartsWith("fleetflow_hardening_",StringComparison.Ordinal)!=true)throw new InvalidOperationException("Unsafe test connection.");
        settings.Database="postgres";var database="fleetflow_notifications_foundation_test_"+Guid.NewGuid().ToString("N")[..12];
        var foundation=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"migrations/notifications/001_foundation.sql"));
        await FoundationMigration.RunAsync(settings.ConnectionString,"notifications",database,foundation,true);
        var owned=new NpgsqlConnectionStringBuilder(settings.ConnectionString){Database=database};
        try{await action(owned.ConnectionString);}
        finally
        {
            using(var pool=new NpgsqlConnection(owned.ConnectionString))NpgsqlConnection.ClearPool(pool);
            await using var admin=new NpgsqlConnection(settings.ConnectionString);await admin.OpenAsync();
            FoundationMigration.ValidateDatabase("notifications",database);
            await using var drop=new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();
        }
    }
}
