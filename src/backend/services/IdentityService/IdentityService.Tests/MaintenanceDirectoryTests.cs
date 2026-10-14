using FleetFlow.Contracts;
using IdentityService.Api.Controllers;
using IdentityService.Api.Data;
using IdentityService.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace IdentityService.Tests;
public sealed class MaintenanceDirectoryTests
{
    [MaintenanceDirectoryPostgresFact] public async Task DirectoryQueriesTranslateAndFilterOnPostgreSql()
    {
        var settings = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FLEET_TEST_POSTGRES"));
        if (settings.Host is not ("127.0.0.1" or "localhost") || settings.Database?.StartsWith("fleetflow_hardening_", StringComparison.Ordinal) != true) throw new InvalidOperationException("Unsafe test target.");
        settings.Database = "postgres"; await using var admin = new Npgsql.NpgsqlConnection(settings.ConnectionString); await admin.OpenAsync();
        var name = "fleetflow_hardening_identitystaff_" + Guid.NewGuid().ToString("N")[..12];
        await using (var create = new Npgsql.NpgsqlCommand($"CREATE DATABASE {name}", admin)) await create.ExecuteNonQueryAsync();
        settings.Database = name;
        try {
            await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(settings.ConnectionString).Options);
            await db.Database.EnsureCreatedAsync(); var role = new Role { Id = Guid.NewGuid(), Name = "MAINTENANCE_STAFF" };
            var user = new User { Id = Guid.NewGuid(), Username = "test-tech", FullName = "Actual technician", Email = "tech@example.test", PasswordHash = "test", Roles = [role] }; db.Users.Add(user); await db.SaveChangesAsync();
            var controller = new MaintenanceStaffController(db) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
            Assert.Equal(user.Id, Assert.Single(Assert.IsType<List<MaintenanceStaffAccount>>(Assert.IsType<OkObjectResult>(await controller.List(default)).Value)).Id);
            Assert.Equal(user.Id, Assert.IsType<MaintenanceStaffAccount>(Assert.IsType<OkObjectResult>(await controller.Get(user.Id, default)).Value).Id);
            user.IsActive = false; await db.SaveChangesAsync(); Assert.IsType<NotFoundResult>(await controller.Get(user.Id, default));
        } finally {
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^fleetflow_hardening_identitystaff_[a-f0-9]{12}$")) throw new InvalidOperationException("Unsafe cleanup.");
            using (var pool = new Npgsql.NpgsqlConnection(settings.ConnectionString)) Npgsql.NpgsqlConnection.ClearPool(pool);
            await using var drop = new Npgsql.NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)", admin); await drop.ExecuteNonQueryAsync();
        }
    }
    [Fact] public async Task DirectoryReturnsOnlyActiveMaintenanceAccountsWithoutPrivateProfileFields()
    {
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var role = new Role { Id = Guid.NewGuid(), Name = "MAINTENANCE_STAFF" }; db.Roles.Add(role);
        var valid = new User { Id = Guid.NewGuid(), FullName = "Maintenance technician", Username = "tech", Email = "private@example.test", PasswordHash = "private", Roles = [role] };
        db.Users.AddRange(valid, new User { Id = Guid.NewGuid(), Username = "inactive", IsActive = false, Roles = [role] }, new User { Id = Guid.NewGuid(), Username = "customer" }); await db.SaveChangesAsync();
        var controller = new MaintenanceStaffController(db) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        var accounts = Assert.IsType<List<MaintenanceStaffAccount>>(Assert.IsType<OkObjectResult>(await controller.List(default)).Value);
        Assert.Equal(valid.Id, Assert.Single(accounts).Id); Assert.IsType<NotFoundResult>(await controller.Get(Guid.NewGuid(), default));
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.DoesNotContain(typeof(MaintenanceStaffAccount).GetProperties(), p => p.Name is "Email" or "PasswordHash" or "DrivingLicenseNumber");
    }
    [Fact] public void DirectoryRequiresRestrictedInternalStaffPermission()
    {
        var policy = Assert.Single(typeof(MaintenanceStaffController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(ServiceScopes.IdentityStaffRead, policy.Policy);
    }
}
public sealed class MaintenanceDirectoryPostgresFactAttribute : FactAttribute
{
    public MaintenanceDirectoryPostgresFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLEET_TEST_POSTGRES"))) Skip = "Requires isolated loopback PostgreSQL."; }
}
