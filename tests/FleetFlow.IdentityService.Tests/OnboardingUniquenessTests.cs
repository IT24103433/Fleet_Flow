using IdentityService.Api.Data;
using IdentityService.Api.Entities;
using IdentityService.Api.Onboarding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace FleetFlow.IdentityService.Tests;
public sealed class OnboardingPostgresFactAttribute:FactAttribute
{public OnboardingPostgresFactAttribute(){if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLEET_TEST_POSTGRES")))Skip="Requires the isolated onboarding regression database.";}}
public sealed class OnboardingUniquenessTests
{
    [OnboardingPostgresFact]public async Task DatabaseArbitratesConcurrentUsernameEmailAndApprovedDocumentOwnership()
    {
        var settings=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FLEET_TEST_POSTGRES"));
        if(settings.Host is not ("localhost" or "127.0.0.1")||settings.Database?.StartsWith("fleetflow_hardening_",StringComparison.Ordinal)!=true)throw new InvalidOperationException("An isolated loopback test connection is required.");
        settings.Database="postgres";await using var admin=new NpgsqlConnection(settings.ConnectionString);await admin.OpenAsync();
        var name="fleetflow_auth_onboarding_test_"+Guid.NewGuid().ToString("N");
        await using(var create=new NpgsqlCommand($"CREATE DATABASE \"{name}\"",admin))await create.ExecuteNonQueryAsync();
        settings.Database=name;var options=new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(settings.ConnectionString).Options;
        try
        {
            await using(var schema=new IdentityDbContext(options))
            {
                await schema.Database.EnsureCreatedAsync();
                await schema.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Users_NormalizedUsername\" ON \"Users\" (lower(trim(\"Username\"))); CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Users_NormalizedEmail\" ON \"Users\" (lower(trim(\"Email\")))");
            }
            async Task<bool> Create(string username,string email)
            {
                await using var db=new IdentityDbContext(options);db.Users.Add(new(){Username=username,Email=email});
                try{await db.SaveChangesAsync();return true;}catch(DbUpdateException ex)when(ex.InnerException is PostgresException{SqlState:"23505"}){return false;}
            }
            Assert.Equal(1,(await Task.WhenAll(Create("Same_User","one@example.test"),Create("same_user","two@example.test"))).Count(x=>x));
            Assert.Equal(1,(await Task.WhenAll(Create("email_user_one","Same@Example.test"),Create("email_user_two","same@example.test"))).Count(x=>x));
            async Task<bool> Claim(Guid owner)
            {
                await using var db=new IdentityDbContext(options);db.DocumentClaims.Add(new(){UserId=owner,Kind="LK:NIC",Fingerprint=new string('A',64),RevisionId=Guid.NewGuid()});
                try{await db.SaveChangesAsync();return true;}catch(DbUpdateException ex)when(ex.InnerException is PostgresException{SqlState:"23505"}){return false;}
            }
            await using(var db=new IdentityDbContext(options))
            {
                var owners=await db.Users.Select(x=>x.Id).ToArrayAsync();Assert.Equal(1,(await Task.WhenAll(Claim(owners[0]),Claim(owners[1]))).Count(x=>x));
                Assert.Single(await db.DocumentClaims.ToListAsync());
            }
        }
        finally
        {
            using(var pool=new NpgsqlConnection(settings.ConnectionString))NpgsqlConnection.ClearPool(pool);
            if(!name.StartsWith("fleetflow_auth_onboarding_test_",StringComparison.Ordinal)||name.Length!="fleetflow_auth_onboarding_test_".Length+32)throw new InvalidOperationException("Unexpected cleanup target.");
            await using var drop=new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();
        }
    }
}
