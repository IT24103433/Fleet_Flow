using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using IdentityService.Api.Data;
using IdentityService.Api.Dtos;
using IdentityService.Api.Entities;
using IdentityService.Api.Exceptions;
using IdentityService.Api.Services;
using Xunit;

namespace IdentityService.Tests;

public class AuthenticationServiceTests
{
    private readonly DbContextOptions<IdentityDbContext> _dbContextOptions;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;
    private const string JwtKey = "AVerySecureKeyThatIsAtLeast32BytesLongChangeInProd";

    public AuthenticationServiceTests()
    {
        _dbContextOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _passwordHasher = new PasswordHasher<User>();

        var inMemorySettings = new Dictionary<string, string>
        {
            {"Jwt:Issuer", "FleetFlow.IdentityService"},
            {"Jwt:Audience", "FleetFlow.Client"},
            {"Jwt:ExpiryInMinutes", "60"},
            {"Jwt:Key", JwtKey}
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
    }

    private async Task SeedUserAsync(string username, string email, string password, string roleName = "CUSTOMER")
    {
        using var context = new IdentityDbContext(_dbContextOptions);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        var role = await context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
        if (role == null)
        {
            role = new Role { Id = Guid.NewGuid(), Name = roleName };
            context.Roles.Add(role);
        }

        user.Roles.Add(role);
        context.Users.Add(user);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task LoginAsync_ValidUsernameAndPassword_Succeeds()
    {
        // Arrange
        await SeedUserAsync("johndoe", "john@example.com", "Password123!");
        using var context = new IdentityDbContext(_dbContextOptions);
        var service = new AuthenticationService(context, _passwordHasher, _configuration);

        var request = new LoginRequest
        {
            UsernameOrEmail = "johndoe",
            Password = "Password123!"
        };

        // Act
        var result = await service.LoginAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);
        Assert.Equal("johndoe", result.User.Username);
        Assert.Equal("john@example.com", result.User.Email);
    }

    [Fact]
    public async Task LoginAsync_ValidEmailAndPassword_Succeeds()
    {
        // Arrange
        await SeedUserAsync("johndoe", "john@example.com", "Password123!");
        using var context = new IdentityDbContext(_dbContextOptions);
        var service = new AuthenticationService(context, _passwordHasher, _configuration);

        var request = new LoginRequest
        {
            UsernameOrEmail = "john@example.com",
            Password = "Password123!"
        };

        // Act
        var result = await service.LoginAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);
        Assert.Equal("johndoe", result.User.Username);
        Assert.Equal("john@example.com", result.User.Email);
    }

    [Fact]
    public async Task LoginAsync_NonExistentUser_ThrowsInvalidCredentialsException()
    {
        // Arrange
        using var context = new IdentityDbContext(_dbContextOptions);
        var service = new AuthenticationService(context, _passwordHasher, _configuration);

        var request = new LoginRequest
        {
            UsernameOrEmail = "nonexistent",
            Password = "Password123!"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.LoginAsync(request));
    }

    [Fact]
    public async Task LoginAsync_IncorrectPassword_ThrowsInvalidCredentialsException()
    {
        // Arrange
        await SeedUserAsync("johndoe", "john@example.com", "Password123!");
        using var context = new IdentityDbContext(_dbContextOptions);
        var service = new AuthenticationService(context, _passwordHasher, _configuration);

        var request = new LoginRequest
        {
            UsernameOrEmail = "johndoe",
            Password = "WrongPassword!"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.LoginAsync(request));
    }

    [Fact]
    public async Task LoginAsync_CaseInsensitiveUsernameLookup_Succeeds()
    {
        // Arrange
        await SeedUserAsync("JohnDoe", "john@example.com", "Password123!");
        using var context = new IdentityDbContext(_dbContextOptions);
        var service = new AuthenticationService(context, _passwordHasher, _configuration);

        var request = new LoginRequest
        {
            UsernameOrEmail = "johndoe",
            Password = "Password123!"
        };

        // Act
        var result = await service.LoginAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("JohnDoe", result.User.Username);
    }

    [Fact]
    public async Task LoginAsync_CaseInsensitiveEmailLookup_Succeeds()
    {
        // Arrange
        await SeedUserAsync("johndoe", "John.Doe@Example.com", "Password123!");
        using var context = new IdentityDbContext(_dbContextOptions);
        var service = new AuthenticationService(context, _passwordHasher, _configuration);

        var request = new LoginRequest
        {
            UsernameOrEmail = "john.doe@example.com",
            Password = "Password123!"
        };

        // Act
        var result = await service.LoginAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("johndoe", result.User.Username);
    }

    [Fact]
    public async Task LoginAsync_ValidRequest_TokenContainsExpectedClaimsAndValidates()
    {
        // Arrange
        await SeedUserAsync("johndoe", "john@example.com", "Password123!");
        using var context = new IdentityDbContext(_dbContextOptions);
        var service = new AuthenticationService(context, _passwordHasher, _configuration);

        var request = new LoginRequest
        {
            UsernameOrEmail = "johndoe",
            Password = "Password123!"
        };

        // Act
        var result = await service.LoginAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Expiration > DateTime.UtcNow);

        // Decode and validate token
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "FleetFlow.IdentityService",
            ValidAudience = "FleetFlow.Client",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey)),
            ClockSkew = TimeSpan.Zero
        };

        var principal = tokenHandler.ValidateToken(result.Token, validationParameters, out var validatedToken);
        Assert.NotNull(validatedToken);
        Assert.NotNull(principal);

        // Claim Checks
        var nameIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
        Assert.NotNull(nameIdClaim);
        Assert.NotEmpty(nameIdClaim.Value);

        var nameClaim = principal.FindFirst(ClaimTypes.Name);
        Assert.NotNull(nameClaim);
        Assert.Equal("johndoe", nameClaim.Value);

        var emailClaim = principal.FindFirst(ClaimTypes.Email);
        Assert.NotNull(emailClaim);
        Assert.Equal("john@example.com", emailClaim.Value);

        var roleClaim = principal.FindFirst(ClaimTypes.Role);
        Assert.NotNull(roleClaim);
        Assert.Equal("CUSTOMER", roleClaim.Value);

        var jtiClaim = principal.FindFirst(JwtRegisteredClaimNames.Jti);
        Assert.NotNull(jtiClaim);
        Assert.NotEmpty(jtiClaim.Value);
    }

    [Fact]
    public async Task LoginAsync_ValidRequest_ExcludesPasswordHashFromResponse()
    {
        // Arrange
        await SeedUserAsync("johndoe", "john@example.com", "Password123!");
        using var context = new IdentityDbContext(_dbContextOptions);
        var service = new AuthenticationService(context, _passwordHasher, _configuration);

        var request = new LoginRequest
        {
            UsernameOrEmail = "johndoe",
            Password = "Password123!"
        };

        // Act
        var result = await service.LoginAsync(request);

        // Assert
        Assert.Equal("johndoe", result.User.Username);
        Assert.Equal("john@example.com", result.User.Email);
        
        var properties = typeof(LoginResponse).GetProperties();
        foreach (var prop in properties)
        {
            Assert.NotEqual("Password", prop.Name);
            Assert.NotEqual("PasswordHash", prop.Name);
        }
        
        var userProperties = typeof(UserResponse).GetProperties();
        foreach (var prop in userProperties)
        {
            Assert.NotEqual("Password", prop.Name);
            Assert.NotEqual("PasswordHash", prop.Name);
        }
    }
}
