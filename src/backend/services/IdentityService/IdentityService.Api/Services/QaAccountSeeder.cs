using IdentityService.Api.Data;
using IdentityService.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace IdentityService.Api.Services;

public static class QaAccountSeeder
{
    public static async Task SeedAsync(IdentityDbContext db, IPasswordHasher<User> hasher, string password)
    {
        foreach (var (username, roleName) in new[] { ("admin", "ADMIN"), ("manager", "FLEET_MANAGER"), ("worker", "MAINTENANCE_STAFF"), ("customer", "CUSTOMER") })
        {
            var email = username + "@fleetflow.io";
            if (await db.Users.AnyAsync(u => u.Username.ToLower() == username || u.Email.ToLower() == email)) continue;
            var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
            if (role == null) continue;
            var customer = roleName == "CUSTOMER";
            var user = new User {
                Id = Guid.NewGuid(), Username = username, Email = email,
                FullName = customer ? "Customer User" : $"{roleName.Replace("_", " ")} User",
                PhoneNumber = customer ? "+1-555-0100" : "+1-555-0199",
                Address = customer ? "100 FleetFlow Operations Center" : "FleetFlow HQ",
                DrivingLicenseNumber = customer ? "DL-QA-10001" : "DL-STAFF-001", CreatedAt = DateTime.UtcNow,
                Roles = [role]
            };
            user.PasswordHash = hasher.HashPassword(user, password); db.Users.Add(user);
        }
        await db.SaveChangesAsync();
    }
}
