using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using IdentityService.Api.Controllers;
using IdentityService.Api.Data;
using IdentityService.Api.Dtos;
using IdentityService.Api.Entities;
using IdentityService.Api.Exceptions;
using IdentityService.Api.Messaging;
using IdentityService.Api.Services;
using Xunit;

namespace IdentityService.Tests;

public class AdminPasswordResetTests
{
    private readonly DbContextOptions<IdentityDbContext> _dbContextOptions;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;
    private const string JwtKey = "AVerySecureKeyThatIsAtLeast32BytesLongChangeInProd";

    public AdminPasswordResetTests()
    {
        _dbContextOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _passwordHasher = new PasswordHasher<User>();

        var inMemorySettings = new Dictionary<string, string>
        {
            { "Jwt:Issuer", "FleetFlow.IdentityService" },
            { "Jwt:Audience", "FleetFlow.Client" },
            { "Jwt:ExpiryInMinutes", "60" },
            { "Jwt:Key", JwtKey }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
    }

    private AdminUserService CreateAdminUserService(IdentityDbContext context)
    {
        var kafkaSettings = Options.Create(new KafkaSettings { Enabled = false });
        return new AdminUserService(context, _passwordHasher, null, kafkaSettings);
    }

    private AuthenticationService CreateAuthService(IdentityDbContext context)
    {
        return new AuthenticationService(context, _passwordHasher, _configuration);
    }

    private async Task<User> SeedUserAsync(string username, string email, string password, string roleName = "CUSTOMER")
    {
        using var context = new IdentityDbContext(_dbContextOptions);

        var role = await context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
        if (role == null)
        {
            role = new Role { Id = Guid.NewGuid(), Name = roleName };
            context.Roles.Add(role);
            await context.SaveChangesAsync();
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            FullName = "Test Target User",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            MustChangePassword = false
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, password);
        user.Roles.Add(role);

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    [Fact]
    public void AdminUsersController_HasAdminAuthorizeAttribute()
    {
        // Assert: Ensure AdminUsersController has [Authorize(Roles = "ADMIN")]
        var authAttr = typeof(AdminUsersController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.Equal("ADMIN", authAttr.Roles);
    }

    [Fact]
    public async Task ResetPasswordAsync_GeneratesSecureTemporaryPassword_AndOverwritesPreviousHash()
    {
        // Arrange
        var initialPassword = "OldOriginalPassword123!";
        var user = await SeedUserAsync("alice_reset", "alice.reset@example.com", initialPassword);
        string oldHash;
        using (var checkContext = new IdentityDbContext(_dbContextOptions))
        {
            var u = await checkContext.Users.FindAsync(user.Id);
            oldHash = u!.PasswordHash;
        }

        using var context = new IdentityDbContext(_dbContextOptions);
        var adminService = CreateAdminUserService(context);

        // Act
        var result = await adminService.ResetPasswordAsync(user.Id, new AdminResetPasswordRequest
        {
            ForcePasswordChange = true
        });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("alice_reset", result.Username);
        Assert.Equal("alice.reset@example.com", result.Email);
        Assert.True(result.MustChangePassword);
        Assert.False(string.IsNullOrWhiteSpace(result.TemporaryPassword));
        Assert.True(result.TemporaryPassword.Length >= 12, "Temporary password must be at least 12 characters.");

        // Complexity checks: must have uppercase, lowercase, digit, and symbol
        Assert.Contains(result.TemporaryPassword, char.IsUpper);
        Assert.Contains(result.TemporaryPassword, char.IsLower);
        Assert.Contains(result.TemporaryPassword, char.IsDigit);
        Assert.True(result.TemporaryPassword.Any(ch => "!@$?*_-".Contains(ch)), "Temporary password must contain a symbol.");

        // Verify database state: hash changed and mustChangePassword is true
        var updatedUser = await context.Users.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.True(updatedUser.MustChangePassword);
        Assert.NotEqual(oldHash, updatedUser.PasswordHash);

        // Verify the temporary password verifies against new hash
        var verifyTemp = _passwordHasher.VerifyHashedPassword(updatedUser, updatedUser.PasswordHash, result.TemporaryPassword);
        Assert.Equal(PasswordVerificationResult.Success, verifyTemp);

        // Verify the old password no longer works
        var verifyOld = _passwordHasher.VerifyHashedPassword(updatedUser, updatedUser.PasswordHash, initialPassword);
        Assert.Equal(PasswordVerificationResult.Failed, verifyOld);
    }

    [Fact]
    public async Task ResetPasswordAsync_DoesNotDiscloseOldPasswordOrHash()
    {
        // Arrange
        var initialPassword = "SecretOriginalPassword999!";
        var user = await SeedUserAsync("bob_nodisclose", "bob.nodisclose@example.com", initialPassword);

        using var context = new IdentityDbContext(_dbContextOptions);
        var adminService = CreateAdminUserService(context);

        // Act
        var result = await adminService.ResetPasswordAsync(user.Id, new AdminResetPasswordRequest());

        // Assert
        Assert.DoesNotContain(initialPassword, result.TemporaryPassword);
        Assert.DoesNotContain(initialPassword, result.Message);
        Assert.DoesNotContain("hash", result.TemporaryPassword, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AQAA", result.TemporaryPassword); // Common ASP.NET Identity v3 salt prefix
    }

    [Fact]
    public async Task ResetPasswordAsync_WithForcePasswordChangeFalse_SetsMustChangePasswordFalse()
    {
        // Arrange
        var user = await SeedUserAsync("charlie_noforce", "charlie@example.com", "SomePassword123!");

        using var context = new IdentityDbContext(_dbContextOptions);
        var adminService = CreateAdminUserService(context);

        // Act
        var result = await adminService.ResetPasswordAsync(user.Id, new AdminResetPasswordRequest
        {
            ForcePasswordChange = false
        });

        // Assert
        Assert.False(result.MustChangePassword);
        var updatedUser = await context.Users.FindAsync(user.Id);
        Assert.False(updatedUser!.MustChangePassword);
    }

    [Fact]
    public async Task ResetPasswordAsync_UserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        using var context = new IdentityDbContext(_dbContextOptions);
        var adminService = CreateAdminUserService(context);
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            adminService.ResetPasswordAsync(nonExistentId, new AdminResetPasswordRequest()));
        Assert.Contains(nonExistentId.ToString(), ex.Message);
    }

    [Fact]
    public async Task FullSecurityFlow_Reset_Login_ForcePasswordChange_SubsequentLogin()
    {
        // 1. Initial State: Customer created with initial password
        var initialPassword = "InitialCustomerPass1!";
        var newPermanentPassword = "NewPermanentPassword456!";
        var user = await SeedUserAsync("dave_flow", "dave.flow@example.com", initialPassword);

        using var context = new IdentityDbContext(_dbContextOptions);
        var adminService = CreateAdminUserService(context);
        var authService = CreateAuthService(context);

        // 2. Admin resets password
        var resetResponse = await adminService.ResetPasswordAsync(user.Id, new AdminResetPasswordRequest
        {
            ForcePasswordChange = true
        });
        var tempPassword = resetResponse.TemporaryPassword;
        Assert.True(resetResponse.MustChangePassword);

        // 3. Old password cannot be used anymore
        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            authService.LoginAsync(new LoginRequest
            {
                UsernameOrEmail = "dave_flow",
                Password = initialPassword,
                LoginChannel = "customer"
            }));

        // 4. Temporary password logs in successfully and flags MustChangePassword = true
        var tempLoginResponse = await authService.LoginAsync(new LoginRequest
        {
            UsernameOrEmail = "dave_flow",
            Password = tempPassword,
            LoginChannel = "customer"
        });
        Assert.NotNull(tempLoginResponse.Token);
        Assert.True(tempLoginResponse.MustChangePassword);
        Assert.True(tempLoginResponse.User.MustChangePassword);

        // 5. User submits forced password change
        await authService.ChangePasswordAsync(user.Id, new ChangePasswordRequest
        {
            NewPassword = newPermanentPassword
        });

        // 6. Temporary password no longer works
        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            authService.LoginAsync(new LoginRequest
            {
                UsernameOrEmail = "dave_flow",
                Password = tempPassword,
                LoginChannel = "customer"
            }));

        // 7. New permanent password succeeds and MustChangePassword is now false
        var permanentLoginResponse = await authService.LoginAsync(new LoginRequest
        {
            UsernameOrEmail = "dave_flow",
            Password = newPermanentPassword,
            LoginChannel = "customer"
        });
        Assert.NotNull(permanentLoginResponse.Token);
        Assert.False(permanentLoginResponse.MustChangePassword);
        Assert.False(permanentLoginResponse.User.MustChangePassword);
    }

    [Fact]
    public async Task ChangePasswordAsync_PasswordTooShort_ThrowsArgumentException()
    {
        // Arrange
        var user = await SeedUserAsync("short_pass_user", "short@example.com", "Password123!");
        using var context = new IdentityDbContext(_dbContextOptions);
        var authService = CreateAuthService(context);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            authService.ChangePasswordAsync(user.Id, new ChangePasswordRequest
            {
                NewPassword = "short"
            }));
        Assert.Contains("at least 8 characters", ex.Message);
    }

    [Fact]
    public async Task ChangePasswordAsync_UserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        using var context = new IdentityDbContext(_dbContextOptions);
        var authService = CreateAuthService(context);
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            authService.ChangePasswordAsync(nonExistentId, new ChangePasswordRequest
            {
                NewPassword = "ValidPassword123!"
            }));
    }
}
