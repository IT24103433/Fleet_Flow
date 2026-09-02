using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using IdentityService.Api.Data;
using IdentityService.Api.Entities;

namespace SeedQaUsers;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var seedPassword = Environment.GetEnvironmentVariable("QA_SEED_PASSWORD");
        if (string.IsNullOrWhiteSpace(seedPassword))
        {
            Console.Error.WriteLine("Error: QA_SEED_PASSWORD environment variable is not set or empty.");
            Console.Error.WriteLine("Please set $env:QA_SEED_PASSWORD before running this tool.");
            return 1;
        }

        var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        var user = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "your_password_here";
        var dbName = Environment.GetEnvironmentVariable("AUTH_DB_NAME") ?? "fleetflow_auth";

        var connectionString = $"Host={host};Port={port};Database={dbName};Username={user};Password={password};";

        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        try
        {
            await using var dbContext = new IdentityDbContext(optionsBuilder.Options);
            await dbContext.Database.EnsureCreatedAsync();

            var passwordHasher = new PasswordHasher<User>();

            // 1. Idempotent Role Seeding
            var defaultRoles = new[] { "CUSTOMER", "FLEET_MANAGER", "MAINTENANCE_STAFF", "ADMIN" };
            foreach (var roleName in defaultRoles)
            {
                var roleExists = await dbContext.Roles.AnyAsync(r => r.Name.ToUpper() == roleName.ToUpper());
                if (!roleExists)
                {
                    dbContext.Roles.Add(new Role
                    {
                        Id = Guid.NewGuid(),
                        Name = roleName
                    });
                }
            }
            await dbContext.SaveChangesAsync();

            // 2. Idempotent QA Accounts Provisioning
            var qaAccounts = new (string Username, string Email, string Role)[]
            {
                ("admin", "admin@fleetflow.io", "ADMIN"),
                ("manager", "manager@fleetflow.io", "FLEET_MANAGER"),
                ("worker", "worker@fleetflow.io", "MAINTENANCE_STAFF"),
                ("customer", "customer@fleetflow.io", "CUSTOMER")
            };

            foreach (var (username, email, roleName) in qaAccounts)
            {
                var role = await dbContext.Roles.FirstAsync(r => r.Name.ToUpper() == roleName.ToUpper());
                var existingUser = await dbContext.Users
                    .Include(u => u.Roles)
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower() || u.Email.ToLower() == email.ToLower());

                if (existingUser == null)
                {
                    var newUser = new User
                    {
                        Id = Guid.NewGuid(),
                        Username = username,
                        Email = email,
                        CreatedAt = DateTime.UtcNow
                    };
                    newUser.PasswordHash = passwordHasher.HashPassword(newUser, seedPassword);
                    newUser.Roles.Add(role);
                    dbContext.Users.Add(newUser);
                }
                else
                {
                    if (!existingUser.Roles.Any(r => r.Name.ToUpper() == roleName.ToUpper()))
                    {
                        existingUser.Roles.Add(role);
                    }
                }
            }

            await dbContext.SaveChangesAsync();

            Console.WriteLine("FleetFlow QA seed completed.");
            Console.WriteLine();
            Console.WriteLine("admin -> ADMIN");
            Console.WriteLine("manager -> FLEET_MANAGER");
            Console.WriteLine("worker -> MAINTENANCE_STAFF");
            Console.WriteLine("customer -> CUSTOMER");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: Failed to seed QA users. {ex.Message}");
            return 1;
        }
    }
}
