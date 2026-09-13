using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using IdentityService.Api.Controllers;
using IdentityService.Api.Data;
using IdentityService.Api.Dtos;
using IdentityService.Api.Entities;
using IdentityService.Api.Exceptions;
using IdentityService.Api.Messaging;
using IdentityService.Api.Middleware;
using IdentityService.Api.Services;
using Xunit;

namespace IdentityService.Tests;

public class ForcedPasswordChangeTests
{
    private readonly DbContextOptions<IdentityDbContext> _dbContextOptions;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;
    private const string JwtKey = "AVerySecureKeyThatIsAtLeast32BytesLongChangeInProd";

    public ForcedPasswordChangeTests()
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

    private async Task<User> SeedUserAsync(string username, string email, string password, string roleName = "CUSTOMER", bool mustChangePassword = false)
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
            FullName = "Test Force Password User",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            MustChangePassword = mustChangePassword
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, password);
        user.Roles.Add(role);

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    [Fact]
    public void ForcePasswordChange_ControllerEndpointRequiresAdminRole()
    {
        // Assert: Ensure AdminUsersController has [Authorize(Roles = "ADMIN")]
        var authAttr = typeof(AdminUsersController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);
        Assert.Equal("ADMIN", authAttr.Roles);

        var endpointMethod = typeof(AdminUsersController).GetMethod(nameof(AdminUsersController.ForcePasswordChange));
        Assert.NotNull(endpointMethod);
    }

    [Fact]
    public async Task SetForcePasswordChangeAsync_EnablesFlagAndPersistsToDatabase()
    {
        // Arrange
        var user = await SeedUserAsync("eva_flag", "eva.flag@example.com", "Password@123", "CUSTOMER", mustChangePassword: false);

        using var context = new IdentityDbContext(_dbContextOptions);
        var adminService = CreateAdminUserService(context);

        // Act: Admin sets force password change to true
        var result = await adminService.SetForcePasswordChangeAsync(user.Id, true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.True(result.MustChangePassword);

        // Verify in database
        var dbUser = await context.Users.FindAsync(user.Id);
        Assert.NotNull(dbUser);
        Assert.True(dbUser.MustChangePassword);
    }

    [Fact]
    public async Task SetForcePasswordChangeAsync_CanClearFlagWhenRequested()
    {
        // Arrange
        var user = await SeedUserAsync("frank_clear", "frank@example.com", "Password@123", "CUSTOMER", mustChangePassword: true);

        using var context = new IdentityDbContext(_dbContextOptions);
        var adminService = CreateAdminUserService(context);

        // Act: Admin clears force password change
        var result = await adminService.SetForcePasswordChangeAsync(user.Id, false);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.MustChangePassword);

        var dbUser = await context.Users.FindAsync(user.Id);
        Assert.NotNull(dbUser);
        Assert.False(dbUser.MustChangePassword);
    }

    [Fact]
    public async Task SetForcePasswordChangeAsync_UserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        using var context = new IdentityDbContext(_dbContextOptions);
        var adminService = CreateAdminUserService(context);
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            adminService.SetForcePasswordChangeAsync(nonExistentId, true));
    }

    [Fact]
    public async Task LoginAsync_IncludesMustChangePasswordClaim_WhenFlagIsTrue()
    {
        // Arrange
        var password = "SecureOriginalPass1!";
        var user = await SeedUserAsync("grace_claim", "grace@example.com", password, "CUSTOMER", mustChangePassword: true);

        using var context = new IdentityDbContext(_dbContextOptions);
        var authService = CreateAuthService(context);

        // Act
        var loginResponse = await authService.LoginAsync(new LoginRequest
        {
            UsernameOrEmail = "grace_claim",
            Password = password,
            LoginChannel = "customer"
        });

        // Assert
        Assert.True(loginResponse.MustChangePassword);
        Assert.True(loginResponse.User.MustChangePassword);
        Assert.NotNull(loginResponse.Token);

        // Validate JWT claim content
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(loginResponse.Token);
        var mustChangeClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "must_change_password");
        Assert.NotNull(mustChangeClaim);
        Assert.Equal("true", mustChangeClaim.Value);
    }

    [Fact]
    public async Task LoginAsync_IncludesMustChangePasswordFalse_WhenFlagIsFalse()
    {
        // Arrange
        var password = "SecureOriginalPass1!";
        var user = await SeedUserAsync("hank_claim", "hank@example.com", password, "CUSTOMER", mustChangePassword: false);

        using var context = new IdentityDbContext(_dbContextOptions);
        var authService = CreateAuthService(context);

        // Act
        var loginResponse = await authService.LoginAsync(new LoginRequest
        {
            UsernameOrEmail = "hank_claim",
            Password = password,
            LoginChannel = "customer"
        });

        // Assert
        Assert.False(loginResponse.MustChangePassword);
        Assert.False(loginResponse.User.MustChangePassword);

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(loginResponse.Token);
        var mustChangeClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "must_change_password");
        Assert.NotNull(mustChangeClaim);
        Assert.Equal("false", mustChangeClaim.Value);
    }

    [Fact]
    public async Task ChangePasswordAsync_ClearsMustChangePasswordFlag_AndEnablesSubsequentNormalLogin()
    {
        // Arrange: User with MustChangePassword = true
        var oldPassword = "TemporaryPass123!";
        var newPassword = "NewPermanentPass456!";
        var user = await SeedUserAsync("ivy_change", "ivy@example.com", oldPassword, "CUSTOMER", mustChangePassword: true);

        using var context = new IdentityDbContext(_dbContextOptions);
        var authService = CreateAuthService(context);

        // Act: User executes change password
        await authService.ChangePasswordAsync(user.Id, new ChangePasswordRequest
        {
            NewPassword = newPassword
        });

        // Assert in DB
        var updatedUser = await context.Users.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.False(updatedUser.MustChangePassword);

        // Subsequent login returns MustChangePassword = false
        var loginRes = await authService.LoginAsync(new LoginRequest
        {
            UsernameOrEmail = "ivy_change",
            Password = newPassword,
            LoginChannel = "customer"
        });
        Assert.False(loginRes.MustChangePassword);
        Assert.False(loginRes.User.MustChangePassword);

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(loginRes.Token);
        var claim = jwt.Claims.FirstOrDefault(c => c.Type == "must_change_password");
        Assert.NotNull(claim);
        Assert.Equal("false", claim.Value);
    }

    [Fact]
    public async Task ChangePasswordAsync_RejectsInvalidShortPassword()
    {
        // Arrange
        var user = await SeedUserAsync("jack_short", "jack@example.com", "Password@123", "CUSTOMER", mustChangePassword: true);

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
    public void ForcePasswordChange_ControllerMethod_HasHttpPostAndCorrectRoute()
    {
        var method = typeof(AdminUsersController).GetMethod(nameof(AdminUsersController.ForcePasswordChange));
        Assert.NotNull(method);

        var httpPostAttr = method.GetCustomAttribute<HttpPostAttribute>();
        Assert.NotNull(httpPostAttr);
        Assert.Equal("{id:guid}/force-password-change", httpPostAttr.Template);
    }

    [Fact]
    public async Task Middleware_BlocksProtectedResources_WhenMustChangePasswordClaimIsTrue()
    {
        // Arrange
        var nextInvoked = false;
        RequestDelegate next = (ctx) =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        };

        var middleware = new ForcedPasswordChangeMiddleware(next);

        var context = new DefaultHttpContext();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "CUSTOMER"),
            new Claim("must_change_password", "true")
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        context.User = new ClaimsPrincipal(identity);
        context.Request.Path = "/api/vehicles/available";

        // Act
        await middleware.InvokeAsync(context);

        // Assert: Access is blocked (403) and pipeline terminated
        Assert.False(nextInvoked);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Middleware_AllowsChangePasswordEndpoint_WhenMustChangePasswordClaimIsTrue()
    {
        // Arrange
        var nextInvoked = false;
        RequestDelegate next = (ctx) =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        };

        var middleware = new ForcedPasswordChangeMiddleware(next);

        var context = new DefaultHttpContext();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("must_change_password", "true")
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        context.User = new ClaimsPrincipal(identity);
        context.Request.Path = "/api/auth/change-password";

        // Act
        await middleware.InvokeAsync(context);

        // Assert: Change-password endpoint is allowed through
        Assert.True(nextInvoked);
    }

    [Fact]
    public async Task Middleware_AllowsProtectedResources_WhenMustChangePasswordClaimIsFalse()
    {
        // Arrange
        var nextInvoked = false;
        RequestDelegate next = (ctx) =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        };

        var middleware = new ForcedPasswordChangeMiddleware(next);

        var context = new DefaultHttpContext();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "CUSTOMER"),
            new Claim("must_change_password", "false")
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        context.User = new ClaimsPrincipal(identity);
        context.Request.Path = "/api/vehicles/available";

        // Act
        await middleware.InvokeAsync(context);

        // Assert: Normal authenticated request proceeds
        Assert.True(nextInvoked);
    }

    [Fact]
    public async Task Middleware_AllowsPublicEndpoints_WhenMustChangePasswordClaimIsTrue()
    {
        // Arrange
        var nextInvoked = false;
        RequestDelegate next = (ctx) =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        };

        var middleware = new ForcedPasswordChangeMiddleware(next);

        var context = new DefaultHttpContext();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("must_change_password", "true")
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        context.User = new ClaimsPrincipal(identity);
        context.Request.Path = "/health";

        // Act
        await middleware.InvokeAsync(context);

        // Assert: Public health route is allowed through
        Assert.True(nextInvoked);
    }
}
