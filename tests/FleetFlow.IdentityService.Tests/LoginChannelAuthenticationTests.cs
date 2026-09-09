using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using IdentityService.Api.Controllers;
using IdentityService.Api.Data;
using IdentityService.Api.Dtos;
using IdentityService.Api.Entities;
using IdentityService.Api.Exceptions;
using IdentityService.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace FleetFlow.IdentityService.Tests;

public class LoginChannelAuthenticationTests
{
    private readonly IConfiguration _configuration;
    private readonly IPasswordHasher<User> _passwordHasher;

    public LoginChannelAuthenticationTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:Issuer", "FleetFlowIdentityService" },
                { "Jwt:Audience", "FleetFlowClients" },
                { "Jwt:Key", "SuperSecretKeyForFleetFlowTestingPurposesOnly1234567890!" },
                { "Jwt:ExpiryInMinutes", "60" }
            })
            .Build();

        _passwordHasher = new PasswordHasher<User>();
    }

    private async Task<(IdentityDbContext dbContext, AuthenticationService authService)> CreateServiceAsync()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new IdentityDbContext(options);

        // Seed roles
        var customerRole = new Role { Id = Guid.NewGuid(), Name = "CUSTOMER" };
        var adminRole = new Role { Id = Guid.NewGuid(), Name = "ADMIN" };
        var fleetManagerRole = new Role { Id = Guid.NewGuid(), Name = "FLEET_MANAGER" };
        var maintenanceStaffRole = new Role { Id = Guid.NewGuid(), Name = "MAINTENANCE_STAFF" };

        dbContext.Roles.AddRange(customerRole, adminRole, fleetManagerRole, maintenanceStaffRole);

        // Seed users
        var customerUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "customer_user",
            Email = "customer@example.com",
            CreatedAt = DateTime.UtcNow,
            Roles = new List<Role> { customerRole }
        };
        customerUser.PasswordHash = _passwordHasher.HashPassword(customerUser, "Password123!");

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "admin_user",
            Email = "admin@example.com",
            CreatedAt = DateTime.UtcNow,
            Roles = new List<Role> { adminRole }
        };
        adminUser.PasswordHash = _passwordHasher.HashPassword(adminUser, "Password123!");

        var fleetManagerUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "fleet_manager_user",
            Email = "fleetmanager@example.com",
            CreatedAt = DateTime.UtcNow,
            Roles = new List<Role> { fleetManagerRole }
        };
        fleetManagerUser.PasswordHash = _passwordHasher.HashPassword(fleetManagerUser, "Password123!");

        var maintenanceUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "maintenance_user",
            Email = "maintenance@example.com",
            CreatedAt = DateTime.UtcNow,
            Roles = new List<Role> { maintenanceStaffRole }
        };
        maintenanceUser.PasswordHash = _passwordHasher.HashPassword(maintenanceUser, "Password123!");

        dbContext.Users.AddRange(customerUser, adminUser, fleetManagerUser, maintenanceUser);
        await dbContext.SaveChangesAsync();

        var service = new AuthenticationService(dbContext, _passwordHasher, _configuration);
        return (dbContext, service);
    }

    // ==========================================
    // Customer Channel Tests
    // ==========================================

    [Fact]
    public async Task CustomerChannel_CustomerRole_SucceedsWithExpectedRoleClaim()
    {
        // Arrange
        var (_, authService) = await CreateServiceAsync();
        var request = new LoginRequest
        {
            UsernameOrEmail = "customer_user",
            Password = "Password123!",
            LoginChannel = "customer"
        };

        // Act
        var response = await authService.LoginAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Token.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(response.Token);
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "CUSTOMER");
    }

    [Fact]
    public async Task CustomerChannel_AdminRole_ThrowsPortalAccessDeniedException()
    {
        // Arrange
        var (_, authService) = await CreateServiceAsync();
        var request = new LoginRequest
        {
            UsernameOrEmail = "admin_user",
            Password = "Password123!",
            LoginChannel = "customer"
        };

        // Act & Assert
        var act = () => authService.LoginAsync(request);
        await act.Should().ThrowAsync<PortalAccessDeniedException>()
            .WithMessage("*Staff and administrator accounts must use the Staff Portal*");
    }

    [Fact]
    public async Task CustomerChannel_FleetManagerRole_ThrowsPortalAccessDeniedException()
    {
        // Arrange
        var (_, authService) = await CreateServiceAsync();
        var request = new LoginRequest
        {
            UsernameOrEmail = "fleet_manager_user",
            Password = "Password123!",
            LoginChannel = "customer"
        };

        // Act & Assert
        var act = () => authService.LoginAsync(request);
        await act.Should().ThrowAsync<PortalAccessDeniedException>()
            .WithMessage("*Staff and administrator accounts must use the Staff Portal*");
    }

    [Fact]
    public async Task CustomerChannel_MaintenanceStaffRole_ThrowsPortalAccessDeniedException()
    {
        // Arrange
        var (_, authService) = await CreateServiceAsync();
        var request = new LoginRequest
        {
            UsernameOrEmail = "maintenance_user",
            Password = "Password123!",
            LoginChannel = "customer"
        };

        // Act & Assert
        var act = () => authService.LoginAsync(request);
        await act.Should().ThrowAsync<PortalAccessDeniedException>()
            .WithMessage("*Staff and administrator accounts must use the Staff Portal*");
    }

    // ==========================================
    // Staff Channel Tests
    // ==========================================

    [Fact]
    public async Task StaffChannel_CustomerRole_ThrowsPortalAccessDeniedException()
    {
        // Arrange
        var (_, authService) = await CreateServiceAsync();
        var request = new LoginRequest
        {
            UsernameOrEmail = "customer_user",
            Password = "Password123!",
            LoginChannel = "staff"
        };

        // Act & Assert
        var act = () => authService.LoginAsync(request);
        await act.Should().ThrowAsync<PortalAccessDeniedException>()
            .WithMessage("*Customer accounts do not have access to the Staff Portal*");
    }

    [Fact]
    public async Task StaffChannel_AdminRole_SucceedsWithExpectedRoleClaim()
    {
        // Arrange
        var (_, authService) = await CreateServiceAsync();
        var request = new LoginRequest
        {
            UsernameOrEmail = "admin_user",
            Password = "Password123!",
            LoginChannel = "staff"
        };

        // Act
        var response = await authService.LoginAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Token.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(response.Token);
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "ADMIN");
    }

    [Fact]
    public async Task StaffChannel_FleetManagerRole_SucceedsWithExpectedRoleClaim()
    {
        // Arrange
        var (_, authService) = await CreateServiceAsync();
        var request = new LoginRequest
        {
            UsernameOrEmail = "fleet_manager_user",
            Password = "Password123!",
            LoginChannel = "staff"
        };

        // Act
        var response = await authService.LoginAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Token.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(response.Token);
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "FLEET_MANAGER");
    }

    [Fact]
    public async Task StaffChannel_MaintenanceStaffRole_SucceedsWithExpectedRoleClaim()
    {
        // Arrange
        var (_, authService) = await CreateServiceAsync();
        var request = new LoginRequest
        {
            UsernameOrEmail = "maintenance_user",
            Password = "Password123!",
            LoginChannel = "staff"
        };

        // Act
        var response = await authService.LoginAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Token.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(response.Token);
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "MAINTENANCE_STAFF");
    }

    // ==========================================
    // Controller HTTP Status Code Mapping Tests
    // ==========================================

    [Fact]
    public async Task AuthController_PortalAccessDenied_ReturnsHttp403Forbidden()
    {
        // Arrange
        var mockRegService = new Mock<IRegistrationService>();
        var mockAuthService = new Mock<IAuthenticationService>();
        mockAuthService
            .Setup(s => s.LoginAsync(It.IsAny<LoginRequest>()))
            .ThrowsAsync(new PortalAccessDeniedException("Staff and administrator accounts must use the Staff Portal to sign in."));

        var controller = new AuthController(mockRegService.Object, mockAuthService.Object);
        var request = new LoginRequest
        {
            UsernameOrEmail = "admin@example.com",
            Password = "Password123!",
            LoginChannel = "customer"
        };

        // Act
        var result = await controller.Login(request);

        // Assert
        var objectResult = result.Result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task AuthController_InvalidCredentials_ReturnsHttp401Unauthorized()
    {
        // Arrange
        var mockRegService = new Mock<IRegistrationService>();
        var mockAuthService = new Mock<IAuthenticationService>();
        mockAuthService
            .Setup(s => s.LoginAsync(It.IsAny<LoginRequest>()))
            .ThrowsAsync(new InvalidCredentialsException());

        var controller = new AuthController(mockRegService.Object, mockAuthService.Object);
        var request = new LoginRequest
        {
            UsernameOrEmail = "customer@example.com",
            Password = "WrongPassword!",
            LoginChannel = "customer"
        };

        // Act
        var result = await controller.Login(request);

        // Assert
        var unauthorizedResult = result.Result as UnauthorizedObjectResult;
        unauthorizedResult.Should().NotBeNull();
        unauthorizedResult!.StatusCode.Should().Be(401);
    }
}
