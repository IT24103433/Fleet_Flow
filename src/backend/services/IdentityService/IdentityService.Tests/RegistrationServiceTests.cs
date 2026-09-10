using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using IdentityService.Api.Data;
using IdentityService.Api.Dtos;
using IdentityService.Api.Entities;
using IdentityService.Api.Exceptions;
using IdentityService.Api.Services;
using Xunit;

namespace IdentityService.Tests;

public class RegistrationServiceTests
{
    private static RegisterRequest CreateValidRegisterRequest(
        string username = "johndoe",
        string email = "john.doe@example.com",
        string fullName = "John Doe",
        string phoneNumber = "+1-555-0199",
        string address = "123 Main St, City, Country",
        string drivingLicenseNumber = "DL-987654321",
        string password = "Password123!") => new()
    {
        FullName = fullName,
        Username = username,
        Email = email,
        PhoneNumber = phoneNumber,
        Address = address,
        DrivingLicenseNumber = drivingLicenseNumber,
        Password = password
    };

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
        
        var request = CreateValidRegisterRequest();

        // Act
        var result = await service.RegisterAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("John Doe", result.FullName);
        Assert.Equal("johndoe", result.Username);
        Assert.Equal("john.doe@example.com", result.Email);
        Assert.Equal("+1-555-0199", result.PhoneNumber);
        Assert.Equal("123 Main St, City, Country", result.Address);
        Assert.Equal("DL-987654321", result.DrivingLicenseNumber);
        
        var dbUser = await context.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == result.Id);
        Assert.NotNull(dbUser);
        Assert.Equal("John Doe", dbUser.FullName);
        Assert.Equal("johndoe", dbUser.Username);
        Assert.Equal("john.doe@example.com", dbUser.Email);
        Assert.Equal("+1-555-0199", dbUser.PhoneNumber);
        Assert.Equal("123 Main St, City, Country", dbUser.Address);
        Assert.Equal("DL-987654321", dbUser.DrivingLicenseNumber);
    }

    [Fact]
    public async Task RegisterAsync_PersistsRealCustomerProfileData()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new IdentityDbContext(options);
        var hasher = new PasswordHasher<User>();
        var service = new RegistrationService(context, hasher);

        var request = CreateValidRegisterRequest(
            fullName: "Jane Alice Smith",
            phoneNumber: "+94 77 123 4567",
            address: "45 Lotus Road, Colombo 03",
            drivingLicenseNumber: "B1234567"
        );

        // Act
        var result = await service.RegisterAsync(request);

        // Assert
        var dbUser = await context.Users.FindAsync(result.Id);
        Assert.NotNull(dbUser);
        Assert.Equal("Jane Alice Smith", dbUser.FullName);
        Assert.Equal("+94 77 123 4567", dbUser.PhoneNumber);
        Assert.Equal("45 Lotus Road, Colombo 03", dbUser.Address);
        Assert.Equal("B1234567", dbUser.DrivingLicenseNumber);
    }

    [Theory]
    [InlineData("", "+1-555-0199", "123 Main St", "DL-12345", "FullName")]
    [InlineData("John Doe", "", "123 Main St", "DL-12345", "PhoneNumber")]
    [InlineData("John Doe", "+1-555-0199", "", "DL-12345", "Address")]
    [InlineData("John Doe", "+1-555-0199", "123 Main St", "", "DrivingLicenseNumber")]
    public async Task RegisterAsync_MissingRequiredProfileFields_ThrowsArgumentException(
        string fullName, string phone, string address, string license, string expectedParam)
    {
        // Arrange
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new IdentityDbContext(options);
        var hasher = new PasswordHasher<User>();
        var service = new RegistrationService(context, hasher);

        var request = CreateValidRegisterRequest(
            fullName: fullName,
            phoneNumber: phone,
            address: address,
            drivingLicenseNumber: license
        );

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterAsync(request));
        Assert.Equal(expectedParam, ex.ParamName);
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
        
        var request = CreateValidRegisterRequest();

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
        
        var request = CreateValidRegisterRequest();

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
            FullName = "Existing User",
            Username = "existinguser",
            Email = "john.doe@example.com",
            PhoneNumber = "+1-555-0100",
            Address = "Existing Address",
            DrivingLicenseNumber = "DL-0000",
            PasswordHash = "hashedpassword"
        });
        await context.SaveChangesAsync();
        
        var request = CreateValidRegisterRequest(
            username: "newuser",
            email: "JOHN.DOE@EXAMPLE.COM"
        );

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
            FullName = "Existing User",
            Username = "JohnDoe",
            Email = "john@example.com",
            PhoneNumber = "+1-555-0100",
            Address = "Existing Address",
            DrivingLicenseNumber = "DL-0000",
            PasswordHash = "hashedpassword"
        });
        await context.SaveChangesAsync();
        
        var request = CreateValidRegisterRequest(
            username: "johndoe",
            email: "newemail@example.com"
        );

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
        
        var request = CreateValidRegisterRequest();

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

        var request = CreateValidRegisterRequest();

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
        
        var request = CreateValidRegisterRequest();

        // Act
        var result = await service.RegisterAsync(request);

        // Assert
        var dbUser = await context.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == result.Id);
        Assert.NotNull(dbUser);
        Assert.Single(dbUser.Roles);
        Assert.Equal("CUSTOMER", dbUser.Roles.First().Name);
    }
}
