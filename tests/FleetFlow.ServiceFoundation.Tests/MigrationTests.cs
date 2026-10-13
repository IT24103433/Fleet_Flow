using FleetFlow.ServiceDefaults;
using Npgsql;
using ServiceDatabaseTool;

namespace FleetFlow.ServiceFoundation.Tests;

public sealed class FoundationPostgresFactAttribute : FactAttribute
{
    public FoundationPostgresFactAttribute()
    {
        if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLEET_TEST_POSTGRES")))
            Skip="Set FLEET_TEST_POSTGRES to an isolated loopback fleetflow_hardening_* database.";
    }
}

public sealed class MigrationTests
{
    [Theory]
    [InlineData("Host=example.com;Database=postgres;Username=test")]
    [InlineData("Host=localhost;Database=fleetflow_fleet;Username=test")]
    public async Task MigrationRefusesRemoteAndLegacyAdminTargetsBeforeConnecting(string connection) =>
        await Assert.ThrowsAsync<InvalidOperationException>(()=>FoundationMigration.RunAsync(
            connection,"rental","fleetflow_rental","",false));

    [FoundationPostgresFact]
    public async Task DryRunDoesNotCreateDatabaseAndApplyIsIdempotentWithVerifiedReadiness()
    {
        await WithDatabase("rental",async (admin,database,sql)=>
        {
            var dry=await FoundationMigration.RunAsync(admin,"rental",database,sql,false);
            Assert.False(dry.DatabaseExists);Assert.False(dry.Applied);
            var applied=await FoundationMigration.RunAsync(admin,"rental",database,sql,true);
            Assert.True(applied.Applied);Assert.True(applied.SchemaInitialized);
            var repeated=await FoundationMigration.RunAsync(admin,"rental",database,sql,true);
            Assert.False(repeated.Applied);Assert.Equal(applied.Checksum,repeated.Checksum);
            var settings=new NpgsqlConnectionStringBuilder(admin){Database=database};
            var descriptor=FoundationMigration.Descriptor("rental") with { Database=database };
            Assert.True(await new DatabaseReadiness(new(settings.ConnectionString,descriptor)).IsReadyAsync(default));
            Assert.False(await new DatabaseReadiness(new(settings.ConnectionString,descriptor with {Name="NotificationService"})).IsReadyAsync(default));
        });
    }
    [FoundationPostgresFact]
    public async Task ChangedChecksumOrOwnershipRefusesInitialization()
    {
        await WithDatabase("notifications",async (admin,database,sql)=>
        {
            await FoundationMigration.RunAsync(admin,"notifications",database,sql,true);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>FoundationMigration.RunAsync(admin,"notifications",database,sql+"-- changed",true));
            var settings=new NpgsqlConnectionStringBuilder(admin){Database=database};
            await using var connection=new NpgsqlConnection(settings.ConnectionString);await connection.OpenAsync();
            await using var command=new NpgsqlCommand("UPDATE \"__FleetFlowServiceSchema\" SET service_name='OtherService'",connection);
            await command.ExecuteNonQueryAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(()=>FoundationMigration.RunAsync(admin,"notifications",database,sql,true));
        });
    }
    [FoundationPostgresFact]
    public async Task ExistingUnrecognizedTablesRemainUntouched()
    {
        await WithDatabase("rental",async (admin,database,sql)=>
        {
            await using var connection=new NpgsqlConnection(admin);await connection.OpenAsync();
            await using(var create=new NpgsqlCommand($"CREATE DATABASE \"{database}\"",connection)) await create.ExecuteNonQueryAsync();
            var settings=new NpgsqlConnectionStringBuilder(admin){Database=database};
            await using var owned=new NpgsqlConnection(settings.ConnectionString);await owned.OpenAsync();
            await using(var create=new NpgsqlCommand("CREATE TABLE sentinel(value text); INSERT INTO sentinel VALUES ('keep')",owned)) await create.ExecuteNonQueryAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(()=>FoundationMigration.RunAsync(admin,"rental",database,sql,true));
            await using var verify=new NpgsqlCommand("SELECT value FROM sentinel",owned);
            Assert.Equal("keep",await verify.ExecuteScalarAsync());
        });
    }
    private static async Task WithDatabase(string service,Func<string,string,string,Task> action)
    {
        var configured=Environment.GetEnvironmentVariable("FLEET_TEST_POSTGRES")!;
        var settings=new NpgsqlConnectionStringBuilder(configured);
        if(settings.Host is not ("localhost" or "127.0.0.1") || settings.Database?.StartsWith("fleetflow_hardening_",StringComparison.Ordinal)!=true)
            throw new InvalidOperationException("Foundation tests require an explicitly isolated local test connection.");
        settings.Database="postgres";
        var descriptor=FoundationMigration.Descriptor(service);
        var database=descriptor.Database+"_foundation_test_"+Guid.NewGuid().ToString("N")[..12];
        FoundationMigration.ValidateDatabase(service,database);
        var sql=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"migrations",service,"001_foundation.sql"));
        try { await action(settings.ConnectionString,database,sql); }
        finally
        {
            var owned=new NpgsqlConnectionStringBuilder(settings.ConnectionString){Database=database};
            using(var pool=new NpgsqlConnection(owned.ConnectionString)) NpgsqlConnection.ClearPool(pool);
            await using var admin=new NpgsqlConnection(settings.ConnectionString);await admin.OpenAsync();
            // Only this test's validated, randomly named database is a permitted cleanup target.
            await using var drop=new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)",admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
