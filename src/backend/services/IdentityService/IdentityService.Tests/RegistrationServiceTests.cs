using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using IdentityService.Api.Data;
using IdentityService.Api.Dtos;
using IdentityService.Api.Entities;
using IdentityService.Api.Exceptions;
using IdentityService.Api.Services;

namespace IdentityService.Tests;

public class RegistrationServiceTests
{
    [Fact]
    public async Task RegisterAsync_ValidRequest_CreatesUserAndReturnsResponse()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        using var context = new IdentityDbContext(options);
        var hasher = new PasswordHasher<User>();
        var service = new RegistrationService(context, hasher);
        
        var request = new RegisterRequest
        {
            Username = "johndoe",
            Email = "john.doe@example.com",
            Password = "Password123!"
        };

        // Act
        var result = await service.RegisterAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("johndoe", result.Username);
        Assert.Equal("john.doe@example.com", result.Email);
        
        var dbUser = await context.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == result.Id);
        Assert.NotNull(dbUser);
        Assert.Equal("johndoe", dbUser.Username);
        Assert.Equal("john.doe@example.com", dbUser.Email);
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_HashesPasswordAndDoesNotStorePlaintext()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        using var context = new IdentityDbContext(options);
        var hasher = new PasswordHasher<User>();
        var service = new RegistrationService(context, hasher);
        
        var request = new RegisterRequest
        {
            Username = "johndoe",
            Email = "john.doe@example.com",
            Password = "Password123!"
        };

        // Act
        var result = await service.RegisterAsync(request);

        // Assert
        var dbUser = await context.Users.FindAsync(result.Id);
        Assert.NotNull(dbUser);
        Assert.NotEqual("Password123!", dbUser.PasswordHash);
        Assert.NotEmpty(dbUser.PasswordHash);
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_PasswordCanBeVerifiedAgainstStoredHash()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        using var context = new IdentityDbContext(options);
        var hasher = new PasswordHasher<User>();
        var service = new RegistrationService(context, hasher);
        
        var request = new RegisterRequest
        {
            Username = "johndoe",
            Email = "john.doe@example.com",
            Password = "Password123!"
        };

        // Act
        var result = await service.RegisterAsync(request);

        // Assert
        var dbUser = await context.Users.FindAsync(result.Id);
        Assert.NotNull(dbUser);
        
        var verificationResult = hasher.VerifyHashedPassword(dbUser, dbUser.PasswordHash, "Password123!");
        Assert.Equal(PasswordVerificationResult.Success, verificationResult);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmailCaseInsensitive_ThrowsDuplicateException()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        using var context = new IdentityDbContext(options);
        var hasher = new PasswordHasher<User>();
        var service = new RegistrationService(context, hasher);
        
        // Add existing user
        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "existinguser",
            Email = "john.doe@example.com",
            PasswordHash = "hashedpassword"
        });
        await context.SaveChangesAsync();
        
        var request = new RegisterRequest
        {
            Username = "newuser",
            Email = "JOHN.DOE@EXAMPLE.COM",
            Password = "Password123!"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DuplicateException>(() => service.RegisterAsync(request));
        Assert.Equal("Email is already registered.", exception.Message);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateUsernameCaseInsensitive_ThrowsDuplicateException()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        using var context = new IdentityDbContext(options);
        var hasher = new PasswordHasher<User>();
        var service = new RegistrationService(context, hasher);
        
        // Add existing user
        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "JohnDoe",
            Email = "john@example.com",
            PasswordHash = "hashedpassword"
        });
        await context.SaveChangesAsync();
        
        var request = new RegisterRequest
        {
            Username = "johndoe",
            Email = "newemail@example.com",
            Password = "Password123!"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DuplicateException>(() => service.RegisterAsync(request));
        Assert.Equal("Username is already taken.", exception.Message);
    }

    [Fact]
    public async Task RegisterAsync_CustomerRoleAbsent_CreatesCustomerRole()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        using var context = new IdentityDbContext(options);
        var hasher = new PasswordHasher<User>();
        var service = new RegistrationService(context, hasher);
        
        var request = new RegisterRequest
        {
            Username = "johndoe",
            Email = "john@example.com",
            Password = "Password123!"
        };

        // Verify role does not exist yet
        var roleExistsBefore = await context.Roles.AnyAsync(r => r.Name == "CUSTOMER");
        Assert.False(roleExistsBefore);

        // Act
        await service.RegisterAsync(request);

        // Assert
        var customerRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "CUSTOMER");
        Assert.NotNull(customerRole);
    }

    [Fact]
    public async Task RegisterAsync_CustomerRoleExists_ReusesExistingRole()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        using var context = new IdentityDbContext(options);
        var hasher = new PasswordHasher<User>();
        var service = new RegistrationService(context, hasher);
        
        var existingRole = new Role
        {
            Id = Guid.NewGuid(),
            Name = "CUSTOMER"
        };
        context.Roles.Add(existingRole);
        await context.SaveChangesAsync();

        var request = new RegisterRequest
        {
            Username = "johndoe",
            Email = "john@example.com",
            Password = "Password123!"
        };

        // Act
        var result = await service.RegisterAsync(request);

        // Assert
        var rolesCount = await context.Roles.CountAsync();
        Assert.Equal(1, rolesCount); // Reused the existing role, count is still 1

        var dbUser = await context.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == result.Id);
        Assert.NotNull(dbUser);
        Assert.Single(dbUser.Roles);
        Assert.Equal(existingRole.Id, dbUser.Roles.First().Id);
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_AssociatesUserWithCustomerRole()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        using var context = new IdentityDbContext(options);
        var hasher = new PasswordHasher<User>();
        var service = new RegistrationService(context, hasher);
        
        var request = new RegisterRequest
        {
            Username = "johndoe",
            Email = "john@example.com",
            Password = "Password123!"
        };

        // Act
        var result = await service.RegisterAsync(request);

        // Assert
        var dbUser = await context.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == result.Id);
        Assert.NotNull(dbUser);
        Assert.Single(dbUser.Roles);
        Assert.Equal("CUSTOMER", dbUser.Roles.First().Name);
    }
}
