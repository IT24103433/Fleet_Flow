using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using IdentityService.Api.Data;
using IdentityService.Api.Entities;
using Xunit;

namespace IdentityService.Tests;

public class RbacSeedingTests
{
    private readonly DbContextOptions<IdentityDbContext> _dbContextOptions;

    public RbacSeedingTests()
    {
        _dbContextOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    private async Task SeedRolesAsync(IdentityDbContext dbContext)
    {
        var defaultRoles = new[] { "CUSTOMER", "FLEET_MANAGER", "ADMIN" };
        bool hasChanges = false;
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
                hasChanges = true;
            }
        }
        if (hasChanges)
        {
            await dbContext.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task SeedRoles_OnEmptyDatabase_SeedsAllThreeDefaultRoles()
    {
        // Arrange
        using var context = new IdentityDbContext(_dbContextOptions);

        // Act
        await SeedRolesAsync(context);

        // Assert
        var roles = await context.Roles.ToListAsync();
        Assert.Equal(3, roles.Count);
        Assert.Contains(roles, r => r.Name == "CUSTOMER");
        Assert.Contains(roles, r => r.Name == "FLEET_MANAGER");
        Assert.Contains(roles, r => r.Name == "ADMIN");
    }

    [Fact]
    public async Task SeedRoles_CalledMultipleTimes_IsIdempotentAndDoesNotDuplicate()
    {
        // Arrange
        using var context = new IdentityDbContext(_dbContextOptions);

        // Act
        await SeedRolesAsync(context);
        await SeedRolesAsync(context);
        await SeedRolesAsync(context);

        // Assert
        var roles = await context.Roles.ToListAsync();
        Assert.Equal(3, roles.Count);
    }
}
