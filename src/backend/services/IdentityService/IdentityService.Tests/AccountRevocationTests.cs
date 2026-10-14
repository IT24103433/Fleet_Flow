using System.IdentityModel.Tokens.Jwt;
using IdentityService.Api.Data;
using IdentityService.Api.Dtos;
using IdentityService.Api.Entities;
using IdentityService.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace IdentityService.Tests;
public sealed class AccountRevocationTests
{
    private static readonly PasswordHasher<User> Hasher = new();
    private static readonly IConfiguration Config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
        ["Jwt:Key"] = "revocation-tests-private-user-key-123456789012345", ["Jwt:Issuer"] = "FleetFlow.IdentityService", ["Jwt:Audience"] = "FleetFlow.Client" }).Build();
    private static async Task<User> Seed(IdentityDbContext db, bool forced = false, string name = "customer", string roleName = "CUSTOMER", bool active = true)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == roleName) ?? new Role { Id = Guid.NewGuid(), Name = roleName };
        var user = new User { Id = Guid.NewGuid(), Username = name, Email = name+"@example.test", FullName = "Test account", IsActive = active, MustChangePassword = forced, Roles = [role] };
        user.PasswordHash = Hasher.HashPassword(user, "Password@123"); db.Users.Add(user); await db.SaveChangesAsync(); return user;
    }
    private static IdentityDbContext Db(string name) => new(new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(name).Options);
    [Fact] public async Task RestartSeedingNeverRestoresOldPasswordsRolesOrDisabledAccounts()
    {
        await using var db = Db(Guid.NewGuid().ToString()); var user = await Seed(db, name: "customer", roleName: "FLEET_MANAGER", active: false);
        user.AccessVersion = 7; user.MustChangePassword = true; await db.SaveChangesAsync();
        var hash = user.PasswordHash;
        await QaAccountSeeder.SeedAsync(db, Hasher, "DifferentSeed@123");
        Assert.Equal(hash, user.PasswordHash); Assert.Equal(7, user.AccessVersion); Assert.False(user.IsActive);
        Assert.True(user.MustChangePassword); Assert.Equal("FLEET_MANAGER", Assert.Single(user.Roles).Name);
    }
    [MaintenanceDirectoryPostgresFact] public async Task PostgreSqlSerializesAdministratorChangesAndRejectsStaleCriticalWrites()
    {
        var settings = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FLEET_TEST_POSTGRES"));
        if (settings.Host is not ("127.0.0.1" or "localhost") || settings.Database?.StartsWith("fleetflow_hardening_", StringComparison.Ordinal) != true) throw new InvalidOperationException("Unsafe test target.");
        settings.Database = "postgres"; await using var admin = new Npgsql.NpgsqlConnection(settings.ConnectionString); await admin.OpenAsync();
        var name = "fleetflow_hardening_access_" + Guid.NewGuid().ToString("N")[..12];
        await using (var create = new Npgsql.NpgsqlCommand($"CREATE DATABASE {name}", admin)) await create.ExecuteNonQueryAsync();
        settings.Database = name;
        IdentityDbContext Open() => new(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(settings.ConnectionString).Options);
        try {
            await using var seed = Open(); await seed.Database.EnsureCreatedAsync();
            var firstAdmin = await Seed(seed, name: "first-admin", roleName: "ADMIN");
            var secondAdmin = await Seed(seed, name: "second-admin", roleName: "ADMIN");
            await using var first = Open(); await using var second = Open();
            async Task<bool> Disable(IdentityDbContext db, Guid id) {
                try { await new AdminUserService(db, Hasher).SetUserStatusAsync(id, false, Guid.NewGuid()); return true; }
                catch (InvalidOperationException) { return false; }
            }
            var results = await Task.WhenAll(Disable(first, firstAdmin.Id), Disable(second, secondAdmin.Id));
            Assert.Single(results, success => success);
            await using var check = Open(); Assert.Equal(1, await check.Users.CountAsync(u => u.IsActive));
            Assert.Equal(1, await check.Users.SumAsync(u => u.AccessVersion));
            var liveId = await check.Users.Where(u => u.IsActive).Select(u => u.Id).SingleAsync();
            await using var a = Open(); await using var b = Open();
            var liveA = (await a.Users.FindAsync(liveId))!; var liveB = (await b.Users.FindAsync(liveId))!;
            liveA.AccessVersion++; liveA.MustChangePassword = true; await a.SaveChangesAsync();
            liveB.AccessVersion++; liveB.PasswordHash = "stale-write";
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync());
            var access = await new AccountAccessReader(check).ReadAsync(liveId, default);
            Assert.True(access!.MustChangePassword); Assert.Equal(1, access.AccessVersion);
        } finally {
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^fleetflow_hardening_access_[a-f0-9]{12}$")) throw new InvalidOperationException("Unsafe cleanup.");
            using (var pool = new Npgsql.NpgsqlConnection(settings.ConnectionString)) Npgsql.NpgsqlConnection.ClearPool(pool);
            await using var drop = new Npgsql.NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }
    [Fact] public async Task PasswordChangeAdvancesVersionAndRenewalUsesCurrentState()
    {
        await using var db = Db(Guid.NewGuid().ToString()); var user = await Seed(db, true); var service = new AuthenticationService(db, Hasher, Config);
        var before = await service.LoginAsync(new() { UsernameOrEmail = user.Username, Password = "Password@123", LoginChannel = "customer" });
        Assert.Equal("0", new JwtSecurityTokenHandler().ReadJwtToken(before.Token).Claims.Single(c => c.Type == "access_version").Value);
        await service.ChangePasswordAsync(user.Id, new() { NewPassword = "NewPassword@123" });
        var after = (await service.RenewSessionAsync(user.Id))!;
        Assert.Equal("1", new JwtSecurityTokenHandler().ReadJwtToken(after.Token).Claims.Single(c => c.Type == "access_version").Value);
        Assert.False(after.MustChangePassword); Assert.Equal(1, (await new AccountAccessReader(db).ReadAsync(user.Id, default))!.AccessVersion);
    }
    [Fact] public async Task OrdinaryPasswordChangeRequiresAndVerifiesCurrentPasswordWithoutPartialUpdates()
    {
        await using var db = Db(Guid.NewGuid().ToString()); var user = await Seed(db); var original = user.PasswordHash; var service = new AuthenticationService(db, Hasher, Config);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ChangePasswordAsync(user.Id, new() { NewPassword = "NewPassword@123" }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ChangePasswordAsync(user.Id, new() { NewPassword = "NewPassword@123", CurrentPassword = "Incorrect@123" }));
        Assert.Equal(original, user.PasswordHash); Assert.Equal(0, user.AccessVersion);
        await service.ChangePasswordAsync(user.Id, new() { NewPassword = "NewPassword@123", CurrentPassword = "Password@123" }); Assert.Equal(1, user.AccessVersion);
    }
    [Fact] public async Task RoleStatusAndResetChangesAdvanceOnceWhileNoOpStatusDoesNot()
    {
        await using var db = Db(Guid.NewGuid().ToString()); var user = await Seed(db); var service = new AdminUserService(db, Hasher);
        await service.SetUserStatusAsync(user.Id, true, Guid.NewGuid()); Assert.Equal(0, user.AccessVersion);
        await service.SetUserStatusAsync(user.Id, false, Guid.NewGuid()); Assert.Equal(1, user.AccessVersion);
        await service.SetUserStatusAsync(user.Id, true, Guid.NewGuid()); Assert.Equal(2, user.AccessVersion);
        await service.UpdateUserAsync(user.Id, new() { Username = user.Username, Email = user.Email, FullName = user.FullName, Role = "FLEET_MANAGER" }, Guid.NewGuid()); Assert.Equal(3, user.AccessVersion);
        await service.ResetPasswordAsync(user.Id, new() { CustomTemporaryPassword = "Temporary@123", ForcePasswordChange = true }, Guid.NewGuid()); Assert.Equal(4, user.AccessVersion);
        Assert.Null(await new AuthenticationService(db, Hasher, Config).RenewSessionAsync(user.Id));
    }
    [Fact] public async Task ConcurrencyTokenPreventsCriticalChangesFromOverwritingEachOther()
    {
        var name = Guid.NewGuid().ToString(); await using var seed = Db(name); var user = await Seed(seed);
        await using var first = Db(name); await using var second = Db(name);
        var a = (await first.Users.FindAsync(user.Id))!; var b = (await second.Users.FindAsync(user.Id))!;
        a.AccessVersion++; a.IsActive = false; await first.SaveChangesAsync();
        b.AccessVersion++; b.PasswordHash = "Must not overwrite"; await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        await using var check = Db(name); var saved = (await check.Users.FindAsync(user.Id))!; Assert.False(saved.IsActive); Assert.NotEqual(b.PasswordHash, saved.PasswordHash);
    }
    [Fact] public async Task DisabledAdministratorsDoNotPermitRemovingTheLastActiveAdministrator()
    {
        await using var db = Db(Guid.NewGuid().ToString()); var active = await Seed(db, name: "active-admin", roleName: "ADMIN"); await Seed(db, name: "disabled-admin", roleName: "ADMIN", active: false);
        var service = new AdminUserService(db, Hasher);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteUserAsync(active.Id, Guid.NewGuid()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateUserAsync(active.Id, new() { Username = active.Username, Email = active.Email, FullName = active.FullName, Role = "FLEET_MANAGER" }, Guid.NewGuid()));
        Assert.Equal(0, active.AccessVersion); Assert.True(active.IsActive);
    }
}
