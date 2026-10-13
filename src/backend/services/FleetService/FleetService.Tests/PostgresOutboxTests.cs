using System.Net;
using FleetFlow.ServiceDefaults;
using FleetService.Api.Data;
using FleetService.Api.Entities;
using FleetService.Api.Messaging;
using FleetService.Api.Messaging.Events;
using FleetService.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
namespace FleetService.Tests;
public sealed class PostgresOutboxTests
{
    [PostgresFact]public async Task OutboxFailureRollsBackTheBookingTransaction()
    {
        await WithDatabase(async connection=>
        {
            Guid vehicleId;
            await using(var db=Context(connection))
            {
                var category=new VehicleCategory{Id=Guid.NewGuid(),Name="Outbox test"};
                var vehicle=new Vehicle{Id=Guid.NewGuid(),VehicleCategoryId=category.Id,Vin=Guid.NewGuid().ToString("N")[..17],LicensePlate="OB-TEST",Make="Test",Model="Test",DailyRate=100,
                    FuelType="Petrol",Transmission="Automatic",SeatingCapacity="5",HubLocation="Test"};vehicleId=vehicle.Id;
                db.AddRange(category,vehicle);await db.SaveChangesAsync();
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"NotificationOutbox\" ADD CONSTRAINT test_outbox_failure CHECK (\"EventType\" <> 'BookingCreatedEvent')");
                await Assert.ThrowsAsync<DbUpdateException>(()=>new BookingService(db).CreateBookingAsync(Guid.NewGuid(),new(){VehicleId=vehicleId,StartDateTime=DateTime.UtcNow.AddDays(1),EndDateTime=DateTime.UtcNow.AddDays(2)}));
            }
            await using var check=Context(connection);Assert.Empty(await check.Bookings.ToListAsync());Assert.Empty(await check.NotificationOutbox.ToListAsync());
            Assert.Equal(VehicleStatus.Available,(await check.Vehicles.FindAsync(vehicleId))!.Status);
        });
    }
    [PostgresFact]public async Task ConcurrentWorkersLeaseOnceAndRestartRecoversRetry()
    {
        await WithDatabase(async connection=>
        {
            var clock=new Clock();var handler=new Handler();var services=new ServiceCollection();
            services.AddDbContext<FleetDbContext>(o=>o.UseNpgsql(connection));services.AddSingleton(new NotificationRouting(true,new Uri("http://localhost:5004/")));
            services.AddSingleton<IHttpClientFactory>(new Clients(handler));services.AddSingleton<IServiceTokenProvider>(new Token());
            using var provider=services.BuildServiceProvider();Guid id;
            await using(var db=Context(connection))
            {
                var row=NotificationOutbox.From(new BookingCreatedEvent{EventId=Guid.NewGuid(),OccurredAt=DateTime.UtcNow,BookingId=Guid.NewGuid(),VehicleId=Guid.NewGuid(),CustomerId=Guid.NewGuid()});id=row.Id;
                db.NotificationOutbox.Add(row);await db.SaveChangesAsync();
            }
            using(var first=Worker(provider,clock))using(var second=Worker(provider,clock))
            {var counts=await Task.WhenAll(first.DispatchBatchAsync(),second.DispatchBatchAsync());Assert.Equal(1,counts.Sum());}
            Assert.Equal(1,handler.Calls);
            handler.Fail=false;clock.Advance(TimeSpan.FromSeconds(3));
            using(var restarted=Worker(provider,clock))Assert.Equal(1,await restarted.DispatchBatchAsync());
            await using var verify=Context(connection);var result=await verify.NotificationOutbox.FindAsync(id);
            Assert.Equal("Delivered",result!.Status);Assert.Equal(2,result.Attempts);Assert.NotNull(result.InboxDeliveredAt);Assert.NotNull(result.DeliveredAt);
            Assert.Equal(2,handler.Calls);
        });
    }
    private static NotificationEventDispatcher Worker(ServiceProvider provider,TimeProvider clock)=>new(provider.GetRequiredService<IServiceScopeFactory>(),new Producer(),Options.Create(new KafkaSettings()),NullLogger<NotificationEventDispatcher>.Instance,clock);
    private static FleetDbContext Context(string connection)=>new(new DbContextOptionsBuilder<FleetDbContext>().UseNpgsql(connection).Options);
    private static async Task WithDatabase(Func<string,Task> action)
    {
        var settings=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FLEET_TEST_POSTGRES"));
        if(settings.Host is not ("localhost" or "127.0.0.1") || settings.Database?.StartsWith("fleetflow_hardening_",StringComparison.Ordinal)!=true)throw new InvalidOperationException("Unsafe test database.");
        settings.Database="postgres";var name="fleetflow_hardening_outbox_"+Guid.NewGuid().ToString("N")[..12];
        await using(var admin=new NpgsqlConnection(settings.ConnectionString))
        {await admin.OpenAsync();await using var create=new NpgsqlCommand($"CREATE DATABASE \"{name}\"",admin);await create.ExecuteNonQueryAsync();}
        var owned=new NpgsqlConnectionStringBuilder(settings.ConnectionString){Database=name};
        try{await using(var db=Context(owned.ConnectionString))await db.Database.EnsureCreatedAsync();await action(owned.ConnectionString);}
        finally
        {
            if(!System.Text.RegularExpressions.Regex.IsMatch(name,"^fleetflow_hardening_outbox_[a-f0-9]{12}$"))throw new InvalidOperationException("Unsafe cleanup target.");
            using(var pool=new NpgsqlConnection(owned.ConnectionString))NpgsqlConnection.ClearPool(pool);
            await using var admin=new NpgsqlConnection(settings.ConnectionString);await admin.OpenAsync();
            await using var drop=new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();
        }
    }
    private sealed class Clock:TimeProvider{private DateTimeOffset now=DateTimeOffset.UtcNow.AddSeconds(1);public override DateTimeOffset GetUtcNow()=>now;public void Advance(TimeSpan time)=>now+=time;}
    private sealed class Handler:HttpMessageHandler{public int Calls;public bool Fail=true;protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message,CancellationToken ct)
    {Interlocked.Increment(ref Calls);using var json=System.Text.Json.JsonDocument.Parse(await message.Content!.ReadAsStringAsync(ct));
     return new HttpResponseMessage(Fail?HttpStatusCode.ServiceUnavailable:HttpStatusCode.OK){Content=System.Net.Http.Json.JsonContent.Create(new {eventId=json.RootElement.GetProperty("eventId").GetGuid(),processed=true})};}}
    private sealed class Clients(HttpMessageHandler handler):IHttpClientFactory{public HttpClient CreateClient(string name)=>new(handler,false){BaseAddress=new Uri("http://localhost:5004/")};}
    private sealed class Token:IServiceTokenProvider{public Task<string> GetTokenAsync(CancellationToken ct)=>Task.FromResult("service-token");}
    private sealed class Producer:IKafkaProducerService{public Task PublishAsync<T>(string topic,string key,T message,CancellationToken cancellationToken=default)=>Task.CompletedTask;}
}
