using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using IdentityService.Api.Controllers;
using IdentityService.Api.Data;
using IdentityService.Api.Dtos;
using IdentityService.Api.Entities;
using IdentityService.Api.Exceptions;
using IdentityService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FleetFlow.IdentityService.Tests;

public class AdminUserManagementTests
{
    private readonly IConfiguration _configuration;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AdminUserManagementTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:Issuer", "FleetFlowIdentityService" },
                { "Jwt:Audience", "FleetFlowClients" },
                { "Jwt:Key", "SuperSecretKeyForAdminUserManagementTestingPurposesOnly1234567890!" },
                { "Jwt:ExpiryInMinutes", "60" }
            })
            .Build();

        _passwordHasher = new PasswordHasher<User>();
    }

    private async Task<(IdentityDbContext dbContext, AdminUserService adminUserService, AdminUsersController controller, AuthenticationService authService)> CreateFixtureAsync()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new IdentityDbContext(options);

        // Seed roles
        var roles = new[]
        {
            new Role { Id = Guid.NewGuid(), Name = "CUSTOMER" },
            new Role { Id = Guid.NewGuid(), Name = "FLEET_MANAGER" },
            new Role { Id = Guid.NewGuid(), Name = "MAINTENANCE_STAFF" },
            new Role { Id = Guid.NewGuid(), Name = "ADMIN" }
        };
        dbContext.Roles.AddRange(roles);
        await dbContext.SaveChangesAsync();

        var adminUserService = new AdminUserService(dbContext, _passwordHasher);
        var controller = new AdminUsersController(adminUserService);
        var authService = new AuthenticationService(dbContext, _passwordHasher, _configuration);

        return (dbContext, adminUserService, controller, authService);
    }

    // ==========================================
    // 1. ADMIN CREATE: All 4 Supported Roles
    // ==========================================

    [Fact]
    public async Task AdminCreate_CustomerRole_Returns201CreatedAndPersistsUser()
    {
        var (dbContext, _, controller, _) = await CreateFixtureAsync();

        var request = new CreateAdminUserRequest
        {
            FullName = "Jane Customer",
            Username = "jane_cust",
            Email = "jane.customer@example.com",
            PhoneNumber = "+1-555-0201",
            Address = "123 Maple St",
            DrivingLicenseNumber = "DL-CUST-201",
            InitialPassword = "SecurePassword123!",
            Role = "CUSTOMER"
        };

        var result = await controller.CreateUser(request);

        var objectResult = result.Result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status201Created);

        var response = objectResult.Value as AdminUserResponse;
        response.Should().NotBeNull();
        response!.Username.Should().Be("jane_cust");
        response.Role.Should().Be("CUSTOMER");
        response.Roles.Should().Contain("CUSTOMER");
        response.Status.Should().Be("ACTIVE");
        response.FullName.Should().Be("Jane Customer");

        // DB Verification
        var dbUser = await dbContext.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Username == "jane_cust");
        dbUser.Should().NotBeNull();
        dbUser!.Roles.Should().ContainSingle(r => r.Name == "CUSTOMER");
    }

    [Fact]
    public async Task AdminCreate_FleetManagerRole_Returns201Created()
    {
        var (dbContext, _, controller, _) = await CreateFixtureAsync();

        var request = new CreateAdminUserRequest
        {
            FullName = "Fleet Dispatcher",
            Username = "fleet_disp",
            Email = "disp@fleetflow.io",
            InitialPassword = "FleetPassword123!",
            Role = "FLEET_MANAGER"
        };

        var result = await controller.CreateUser(request);

        var objectResult = result.Result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status201Created);

        var response = objectResult.Value as AdminUserResponse;
        response.Should().NotBeNull();
        response!.Role.Should().Be("FLEET_MANAGER");
        response.FullName.Should().Be("Fleet Dispatcher");

        var dbUser = await dbContext.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Username == "fleet_disp");
        dbUser.Should().NotBeNull();
        dbUser!.Roles.Should().ContainSingle(r => r.Name == "FLEET_MANAGER");
    }

    [Fact]
    public async Task AdminCreate_MaintenanceStaffRole_Returns201Created()
    {
        var (dbContext, _, controller, _) = await CreateFixtureAsync();

        var request = new CreateAdminUserRequest
        {
            FullName = "Service Mechanic",
            Username = "service_tech",
            Email = "tech@fleetflow.io",
            InitialPassword = "TechPassword123!",
            Role = "MAINTENANCE_STAFF"
        };

        var result = await controller.CreateUser(request);

        var objectResult = result.Result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status201Created);

        var response = objectResult.Value as AdminUserResponse;
        response.Should().NotBeNull();
        response!.Role.Should().Be("MAINTENANCE_STAFF");

        var dbUser = await dbContext.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Username == "service_tech");
        dbUser.Should().NotBeNull();
        dbUser!.Roles.Should().ContainSingle(r => r.Name == "MAINTENANCE_STAFF");
    }

    [Fact]
    public async Task AdminCreate_AdminRole_Returns201Created()
    {
        var (dbContext, _, controller, _) = await CreateFixtureAsync();

        var request = new CreateAdminUserRequest
        {
            FullName = "Co-Administrator",
            Username = "co_admin",
            Email = "coadmin@fleetflow.io",
            InitialPassword = "AdminPassword123!",
            Role = "ADMIN"
        };

        var result = await controller.CreateUser(request);

        var objectResult = result.Result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status201Created);

        var response = objectResult.Value as AdminUserResponse;
        response.Should().NotBeNull();
        response!.Role.Should().Be("ADMIN");

        var dbUser = await dbContext.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Username == "co_admin");
        dbUser.Should().NotBeNull();
        dbUser!.Roles.Should().ContainSingle(r => r.Name == "ADMIN");
    }

    // ==========================================
    // 2. AUTHORIZATION: Roles & Attribute Enforcement
    // ==========================================

    [Fact]
    public void AdminUsersController_HasAuthorizeAttributeWithAdminRole()
    {
        var authAttribute = typeof(AdminUsersController).GetCustomAttribute<AuthorizeAttribute>();
        authAttribute.Should().NotBeNull("AdminUsersController must be protected by [Authorize]");
        authAttribute!.Roles.Should().Be("ADMIN", "Only ADMIN role must be permitted on AdminUsersController");
    }

    [Theory]
    [InlineData("CUSTOMER")]
    [InlineData("FLEET_MANAGER")]
    [InlineData("MAINTENANCE_STAFF")]
    public void NonAdminRoles_AreExcludedFromAdminAuthorizationPolicy(string role)
    {
        var authAttribute = typeof(AdminUsersController).GetCustomAttribute<AuthorizeAttribute>();
        var allowedRoles = authAttribute!.Roles!.Split(',').Select(r => r.Trim()).ToList();

        allowedRoles.Should().NotContain(role, $"{role} must be rejected by the controller authorization policy");
    }

    // ==========================================
    // 3. VALIDATION: Role, Duplicate, Password Rules
    // ==========================================

    [Theory]
    [InlineData("SUPERUSER")]
    [InlineData("ROOT")]
    [InlineData("GUEST")]
    [InlineData("UNKNOWN_ROLE")]
    public async Task AdminCreate_InvalidRole_Returns400BadRequest(string invalidRole)
    {
        var (_, _, controller, _) = await CreateFixtureAsync();

        var request = new CreateAdminUserRequest
        {
            FullName = "Hacker User",
            Username = "hacker_user",
            Email = "hacker@example.com",
            InitialPassword = "Password123!",
            Role = invalidRole
        };

        var result = await controller.CreateUser(request);

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task AdminCreate_DuplicateUsername_Returns409Conflict()
    {
        var (_, _, controller, _) = await CreateFixtureAsync();

        var request1 = new CreateAdminUserRequest
        {
            Username = "existing_user",
            Email = "user1@example.com",
            InitialPassword = "Password123!",
            Role = "FLEET_MANAGER"
        };
        await controller.CreateUser(request1);

        var request2 = new CreateAdminUserRequest
        {
            Username = "existing_user", // Duplicate
            Email = "user2@example.com",
            InitialPassword = "Password123!",
            Role = "ADMIN"
        };
        var result = await controller.CreateUser(request2);

        var conflictResult = result.Result as ConflictObjectResult;
        conflictResult.Should().NotBeNull();
        conflictResult!.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task AdminCreate_DuplicateEmail_Returns409Conflict()
    {
        var (_, _, controller, _) = await CreateFixtureAsync();

        var request1 = new CreateAdminUserRequest
        {
            Username = "user_alpha",
            Email = "shared@example.com",
            InitialPassword = "Password123!",
            Role = "FLEET_MANAGER"
        };
        await controller.CreateUser(request1);

        var request2 = new CreateAdminUserRequest
        {
            Username = "user_beta",
            Email = "shared@example.com", // Duplicate
            InitialPassword = "Password123!",
            Role = "MAINTENANCE_STAFF"
        };
        var result = await controller.CreateUser(request2);

        var conflictResult = result.Result as ConflictObjectResult;
        conflictResult.Should().NotBeNull();
        conflictResult!.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task AdminCreate_PasswordTooShort_Returns400BadRequest()
    {
        var (_, _, controller, _) = await CreateFixtureAsync();

        var request = new CreateAdminUserRequest
        {
            Username = "short_pwd_user",
            Email = "short@example.com",
            InitialPassword = "short", // < 8 chars
            Role = "FLEET_MANAGER"
        };

        var result = await controller.CreateUser(request);

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task AdminCreate_CustomerMissingRequiredProfileField_Returns400BadRequest()
    {
        var (_, _, controller, _) = await CreateFixtureAsync();

        // Customer missing DrivingLicenseNumber
        var request = new CreateAdminUserRequest
        {
            FullName = "Incomplete Customer",
            Username = "incomplete_cust",
            Email = "inc@example.com",
            PhoneNumber = "+1-555-0100",
            Address = "123 Road",
            DrivingLicenseNumber = "", // Missing!
            InitialPassword = "Password123!",
            Role = "CUSTOMER"
        };

        var result = await controller.CreateUser(request);

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    // ==========================================
    // 4. PERSISTENCE & SECURITY: Password Hashing & Sanitization
    // ==========================================

    [Fact]
    public async Task AdminCreate_PersistsHashedPassword_NeverStoresOrReturnsPlaintext()
    {
        var (dbContext, _, controller, _) = await CreateFixtureAsync();

        var plainPassword = "SuperSecretPassword123!";
        var request = new CreateAdminUserRequest
        {
            FullName = "Security Test User",
            Username = "sec_test_user",
            Email = "sec@example.com",
            InitialPassword = plainPassword,
            Role = "FLEET_MANAGER"
        };

        var result = await controller.CreateUser(request);
        var objectResult = result.Result as ObjectResult;
        var response = objectResult!.Value as AdminUserResponse;

        // Verify response properties do not contain password or hash
        var responseProps = typeof(AdminUserResponse).GetProperties().Select(p => p.Name).ToList();
        responseProps.Should().NotContain("Password");
        responseProps.Should().NotContain("PasswordHash");
        responseProps.Should().NotContain("Salt");
        responseProps.Should().NotContain("SecurityStamp");

        // Verify database entity has hashed password
        var dbUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == "sec_test_user");
        dbUser.Should().NotBeNull();
        dbUser!.PasswordHash.Should().NotBeNullOrWhiteSpace();
        dbUser.PasswordHash.Should().NotBe(plainPassword, "Plaintext password must never be stored in database");

        // Verify password verification succeeds with correct hasher
        var verifyResult = _passwordHasher.VerifyHashedPassword(dbUser, dbUser.PasswordHash, plainPassword);
        verifyResult.Should().Be(PasswordVerificationResult.Success);
    }

    // ==========================================
    // 5. USER DIRECTORY: GET /api/admin/users
    // ==========================================

    [Fact]
    public async Task AdminGetUsers_ReturnsAllPersistedUsersOrderedNewestFirst()
    {
        var (dbContext, _, controller, _) = await CreateFixtureAsync();

        // Create 3 users
        await controller.CreateUser(new CreateAdminUserRequest
        {
            Username = "first_user",
            Email = "first@example.com",
            InitialPassword = "Password123!",
            Role = "FLEET_MANAGER"
        });

        await Task.Delay(10); // Ensure timestamp difference

        await controller.CreateUser(new CreateAdminUserRequest
        {
            Username = "second_user",
            Email = "second@example.com",
            InitialPassword = "Password123!",
            Role = "ADMIN"
        });

        var getResult = await controller.GetUsers();
        var okResult = getResult.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        var usersList = okResult.Value as List<AdminUserResponse>;
        usersList.Should().NotBeNull();
        usersList!.Count.Should().Be(2);

        // Ordered newest first
        usersList[0].Username.Should().Be("second_user");
        usersList[1].Username.Should().Be("first_user");

        // Safe fields check
        foreach (var u in usersList)
        {
            u.Status.Should().Be("ACTIVE");
            u.Role.Should().NotBeNullOrWhiteSpace();
            u.Roles.Should().NotBeEmpty();
        }
    }

    // ==========================================
    // 6. CHANNEL INTEGRATION: Admin-Created Users Login Rules
    // ==========================================

    [Fact]
    public async Task AdminCreatedCustomer_CanLoginViaCustomerChannel_DeniedFromStaffChannel()
    {
        var (_, _, controller, authService) = await CreateFixtureAsync();

        var password = "CustomerPassword123!";
        await controller.CreateUser(new CreateAdminUserRequest
        {
            FullName = "Admin Created Customer",
            Username = "admin_created_cust",
            Email = "admin_cust@example.com",
            PhoneNumber = "+1-555-0300",
            Address = "456 Oak St",
            DrivingLicenseNumber = "DL-CUST-300",
            InitialPassword = password,
            Role = "CUSTOMER"
        });

        // Customer login channel -> SUCCESS
        var customerLogin = await authService.LoginAsync(new LoginRequest
        {
            UsernameOrEmail = "admin_created_cust",
            Password = password,
            LoginChannel = "customer"
        });
        customerLogin.Should().NotBeNull();
        customerLogin.Token.Should().NotBeNullOrWhiteSpace();

        // Staff login channel -> 403 Forbidden
        var actStaff = () => authService.LoginAsync(new LoginRequest
        {
            UsernameOrEmail = "admin_created_cust",
            Password = password,
            LoginChannel = "staff"
        });
        await actStaff.Should().ThrowAsync<PortalAccessDeniedException>();
    }

    [Fact]
    public async Task AdminCreatedFleetManager_CanLoginViaStaffChannel_DeniedFromCustomerChannel()
    {
        var (_, _, controller, authService) = await CreateFixtureAsync();

        var password = "ManagerPassword123!";
        await controller.CreateUser(new CreateAdminUserRequest
        {
            FullName = "Admin Created Manager",
            Username = "admin_created_mgr",
            Email = "admin_mgr@example.com",
            InitialPassword = password,
            Role = "FLEET_MANAGER"
        });

        // Staff login channel -> SUCCESS
        var staffLogin = await authService.LoginAsync(new LoginRequest
        {
            UsernameOrEmail = "admin_created_mgr",
            Password = password,
            LoginChannel = "staff"
        });
        staffLogin.Should().NotBeNull();
        staffLogin.Token.Should().NotBeNullOrWhiteSpace();

        // Customer login channel -> 403 Forbidden
        var actCustomer = () => authService.LoginAsync(new LoginRequest
        {
            UsernameOrEmail = "admin_created_mgr",
            Password = password,
            LoginChannel = "customer"
        });
        await actCustomer.Should().ThrowAsync<PortalAccessDeniedException>();
    }

    [Fact]
    public async Task AdminCreate_StaffWithoutOptionalProfile_LeavesFieldsEmptyAndNotFaked()
    {
        var (dbContext, _, controller, _) = await CreateFixtureAsync();

        var request = new CreateAdminUserRequest
        {
            Username = "staff_minimal",
            Email = "staff_minimal@fleetflow.io",
            InitialPassword = "MinimalPassword123!",
            Role = "FLEET_MANAGER"
            // FullName, PhoneNumber, Address, DrivingLicenseNumber omitted
        };

        var result = await controller.CreateUser(request);

        var objectResult = result.Result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status201Created);

        var response = objectResult.Value as AdminUserResponse;
        response.Should().NotBeNull();
        response!.FullName.Should().Be(string.Empty);

        var dbUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == "staff_minimal");
        dbUser.Should().NotBeNull();
        dbUser!.FullName.Should().Be(string.Empty);
        dbUser.PhoneNumber.Should().Be(string.Empty);
        dbUser.Address.Should().Be(string.Empty);
        dbUser.DrivingLicenseNumber.Should().Be(string.Empty);
    }

    [Fact]
    public async Task AdminCreate_StaffWithExplicitProfile_PreservesExplicitValues()
    {
        var (dbContext, _, controller, _) = await CreateFixtureAsync();

        var request = new CreateAdminUserRequest
        {
            FullName = "Robert Maintenance",
            Username = "staff_explicit",
            Email = "staff_explicit@fleetflow.io",
            InitialPassword = "ExplicitPassword123!",
            Role = "MAINTENANCE_STAFF",
            PhoneNumber = "+94-77-123-4567",
            Address = "Colombo Hub Depot",
            DrivingLicenseNumber = "B-883921"
        };

        var result = await controller.CreateUser(request);

        var objectResult = result.Result as ObjectResult;
        objectResult.Should().NotBeNull();
        objectResult!.StatusCode.Should().Be(StatusCodes.Status201Created);

        var response = objectResult.Value as AdminUserResponse;
        response.Should().NotBeNull();
        response!.FullName.Should().Be("Robert Maintenance");

        var dbUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == "staff_explicit");
        dbUser.Should().NotBeNull();
        dbUser!.FullName.Should().Be("Robert Maintenance");
        dbUser.PhoneNumber.Should().Be("+94-77-123-4567");
        dbUser.Address.Should().Be("Colombo Hub Depot");
        dbUser.DrivingLicenseNumber.Should().Be("B-883921");
    }

    // ==========================================
    // 8. ADMIN EDIT USER TESTS (Phase 2)
    // ==========================================

    [Fact]
    public async Task AdminEdit_ValidStaffUser_Returns200AndUpdatesFields()
    {
        var (dbContext, _, controller, _) = await CreateFixtureAsync();

        // Create an initial user
        var createRequest = new CreateAdminUserRequest
        {
            FullName = "Alex Initial",
            Username = "alex_init",
            Email = "alex.init@fleetflow.io",
            Role = "MAINTENANCE_STAFF",
            InitialPassword = "InitialPassword123!"
        };
        var createResult = await controller.CreateUser(createRequest);
        var created = (createResult.Result as ObjectResult)!.Value as AdminUserResponse;

        var originalUser = await dbContext.Users.FindAsync(created!.Id);
        var originalId = originalUser!.Id;
        var originalCreatedAt = originalUser.CreatedAt;
        var originalPasswordHash = originalUser.PasswordHash;

        var updateRequest = new UpdateAdminUserRequest
        {
            Username = "alex_updated",
            Email = "alex.updated@fleetflow.io",
            Role = "FLEET_MANAGER",
            FullName = "Alex Fleet Manager",
            PhoneNumber = "+94-77-999-8888",
            Address = "Colombo Port Terminal",
            DrivingLicenseNumber = ""
        };

        var currentAdminId = Guid.NewGuid();
        var updateResult = await controller.UpdateUser(created.Id, updateRequest);

        var okResult = updateResult.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);

        var response = okResult.Value as AdminUserResponse;
        response.Should().NotBeNull();
        response!.Username.Should().Be("alex_updated");
        response.Email.Should().Be("alex.updated@fleetflow.io");
        response.Role.Should().Be("FLEET_MANAGER");
        response.FullName.Should().Be("Alex Fleet Manager");
        response.PhoneNumber.Should().Be("+94-77-999-8888");

        // Verify persistence in DB
        var updatedInDb = await dbContext.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == created.Id);
        updatedInDb.Should().NotBeNull();
        updatedInDb!.Username.Should().Be("alex_updated");
        updatedInDb.Email.Should().Be("alex.updated@fleetflow.io");
        updatedInDb.Roles.Should().ContainSingle(r => r.Name == "FLEET_MANAGER");

        // Verify immutability of Id, CreatedAt, PasswordHash
        updatedInDb.Id.Should().Be(originalId);
        updatedInDb.CreatedAt.Should().Be(originalCreatedAt);
        updatedInDb.PasswordHash.Should().Be(originalPasswordHash);
    }

    [Fact]
    public async Task AdminEdit_NonExistentUser_Returns404NotFound()
    {
        var (_, _, controller, _) = await CreateFixtureAsync();

        var missingId = Guid.NewGuid();
        var updateRequest = new UpdateAdminUserRequest
        {
            Username = "non_existent",
            Email = "nonexistent@fleetflow.io",
            Role = "FLEET_MANAGER"
        };

        var result = await controller.UpdateUser(missingId, updateRequest);

        var notFoundResult = result.Result as NotFoundObjectResult;
        notFoundResult.Should().NotBeNull();
        notFoundResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task AdminEdit_DuplicateUsername_Returns409Conflict()
    {
        var (dbContext, _, controller, _) = await CreateFixtureAsync();

        var user1 = new User
        {
            Id = Guid.NewGuid(),
            Username = "user_one",
            Email = "user_one@fleetflow.io",
            PasswordHash = "hash1"
        };
        var user2 = new User
        {
            Id = Guid.NewGuid(),
            Username = "user_two",
            Email = "user_two@fleetflow.io",
            PasswordHash = "hash2"
        };
        dbContext.Users.AddRange(user1, user2);
        await dbContext.SaveChangesAsync();

        var updateRequest = new UpdateAdminUserRequest
        {
            Username = "USER_TWO", // Case-insensitive duplicate
            Email = "user_one_new@fleetflow.io",
            Role = "FLEET_MANAGER"
        };

        var result = await controller.UpdateUser(user1.Id, updateRequest);

        var conflictResult = result.Result as ConflictObjectResult;
        conflictResult.Should().NotBeNull();
        conflictResult!.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task AdminEdit_DuplicateEmail_Returns409Conflict()
    {
        var (dbContext, _, controller, _) = await CreateFixtureAsync();

        var user1 = new User
        {
            Id = Guid.NewGuid(),
            Username = "user_alpha",
            Email = "alpha@fleetflow.io",
            PasswordHash = "hash1"
        };
        var user2 = new User
        {
            Id = Guid.NewGuid(),
            Username = "user_beta",
            Email = "beta@fleetflow.io",
            PasswordHash = "hash2"
        };
        dbContext.Users.AddRange(user1, user2);
        await dbContext.SaveChangesAsync();

        var updateRequest = new UpdateAdminUserRequest
        {
            Username = "user_alpha",
            Email = "BETA@fleetflow.io", // Case-insensitive duplicate
            Role = "FLEET_MANAGER"
        };

        var result = await controller.UpdateUser(user1.Id, updateRequest);

        var conflictResult = result.Result as ConflictObjectResult;
        conflictResult.Should().NotBeNull();
        conflictResult!.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task AdminEdit_RetainingOwnUsernameAndEmail_Returns200Ok()
    {
        var (dbContext, _, controller, _) = await CreateFixtureAsync();

        var adminRole = await dbContext.Roles.FirstAsync(r => r.Name == "ADMIN");
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "same_user",
            Email = "same@fleetflow.io",
            FullName = "Before Name",
            PasswordHash = "hash"
        };
        user.Roles.Add(adminRole);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var updateRequest = new UpdateAdminUserRequest
        {
            Username = "same_user",
            Email = "same@fleetflow.io",
            Role = "ADMIN",
            FullName = "After Name"
        };

        var result = await controller.UpdateUser(user.Id, updateRequest);

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task AdminEdit_InvalidRole_Returns400BadRequest()
    {
        var (dbContext, _, controller, _) = await CreateFixtureAsync();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "target_user",
            Email = "target@fleetflow.io",
            PasswordHash = "hash"
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var updateRequest = new UpdateAdminUserRequest
        {
            Username = "target_user",
            Email = "target@fleetflow.io",
            Role = "INVALID_SUPER_ROLE"
        };

        var result = await controller.UpdateUser(user.Id, updateRequest);

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task AdminEdit_ToCustomerRole_MissingRequiredFields_Returns400BadRequest()
    {
        var (dbContext, _, controller, _) = await CreateFixtureAsync();

        var staffRole = await dbContext.Roles.FirstAsync(r => r.Name == "FLEET_MANAGER");
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "staff_to_cust",
            Email = "staff2cust@fleetflow.io",
            PasswordHash = "hash"
        };
        user.Roles.Add(staffRole);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var updateRequest = new UpdateAdminUserRequest
        {
            Username = "staff_to_cust",
            Email = "staff2cust@fleetflow.io",
            Role = "CUSTOMER",
            FullName = "", // Missing
            PhoneNumber = "+94-77-111-2222",
            Address = "Colombo",
            DrivingLicenseNumber = "B-12345"
        };

        var result = await controller.UpdateUser(user.Id, updateRequest);

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task AdminEdit_SelfDemotion_Returns400BadRequest()
    {
        var (dbContext, _, controller, _) = await CreateFixtureAsync();

        var adminRole = await dbContext.Roles.FirstAsync(r => r.Name == "ADMIN");
        var currentAdminId = Guid.NewGuid();
        var adminUser = new User
        {
            Id = currentAdminId,
            Username = "acting_admin",
            Email = "acting.admin@fleetflow.io",
            PasswordHash = "hash"
        };
        adminUser.Roles.Add(adminRole);
        dbContext.Users.Add(adminUser);
        await dbContext.SaveChangesAsync();

        // Set user context in controller to simulate current logged-in admin
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, currentAdminId.ToString()),
            new(ClaimTypes.Name, "acting_admin"),
            new(ClaimTypes.Role, "ADMIN")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        var updateRequest = new UpdateAdminUserRequest
        {
            Username = "acting_admin",
            Email = "acting.admin@fleetflow.io",
            Role = "FLEET_MANAGER" // Self-demotion attempt
        };

        var result = await controller.UpdateUser(currentAdminId, updateRequest);

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task AdminEdit_LastAdminDemotion_Returns400BadRequest()
    {
        var (dbContext, _, controller, _) = await CreateFixtureAsync();

        var adminRole = await dbContext.Roles.FirstAsync(r => r.Name == "ADMIN");
        var soleAdminId = Guid.NewGuid();
        var adminUser = new User
        {
            Id = soleAdminId,
            Username = "sole_admin",
            Email = "sole.admin@fleetflow.io",
            PasswordHash = "hash"
        };
        adminUser.Roles.Add(adminRole);
        dbContext.Users.Add(adminUser);
        await dbContext.SaveChangesAsync();

        // Set context to a different admin ID (e.g. external token)
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, "ADMIN")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        var updateRequest = new UpdateAdminUserRequest
        {
            Username = "sole_admin",
            Email = "sole.admin@fleetflow.io",
            Role = "MAINTENANCE_STAFF" // Demoting the ONLY admin
        };

        var result = await controller.UpdateUser(soleAdminId, updateRequest);

        var badRequestResult = result.Result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task AdminEdit_ChannelIntegration_RoleChangeUpdatesLoginAccess()
    {
        var (dbContext, _, controller, authService) = await CreateFixtureAsync();

        // 1. Create a CUSTOMER user initially
        var createRequest = new CreateAdminUserRequest
        {
            FullName = "Dynamic Role User",
            Username = "dynamic_user",
            Email = "dynamic@fleetflow.io",
            PhoneNumber = "+94-77-555-4433",
            Address = "100 Main St, Colombo",
            DrivingLicenseNumber = "B-998877",
            InitialPassword = "DynamicPassword123!",
            Role = "CUSTOMER"
        };
        var createResult = await controller.CreateUser(createRequest);
        var created = (createResult.Result as ObjectResult)!.Value as AdminUserResponse;

        // Verify initial customer channel login succeeds
        var customerLogin = await authService.LoginAsync(new LoginRequest
        {
            UsernameOrEmail = "dynamic_user",
            Password = "DynamicPassword123!",
            LoginChannel = "customer"
        });
        customerLogin.Should().NotBeNull();
        customerLogin.User.Username.Should().Be("dynamic_user");

        // Verify initial staff channel login fails (403 PortalAccessDeniedException)
        var staffLoginAttempt1 = async () => await authService.LoginAsync(new LoginRequest
        {
            UsernameOrEmail = "dynamic_user",
            Password = "DynamicPassword123!",
            LoginChannel = "staff"
        });
        await staffLoginAttempt1.Should().ThrowAsync<PortalAccessDeniedException>();

        // 2. Admin promotes user to FLEET_MANAGER
        var updateRequest = new UpdateAdminUserRequest
        {
            Username = "dynamic_user",
            Email = "dynamic@fleetflow.io",
            Role = "FLEET_MANAGER",
            FullName = "Dynamic Role User"
        };
        var updateResult = await controller.UpdateUser(created!.Id, updateRequest);
        var updatedObj = updateResult.Result as OkObjectResult;
        updatedObj.Should().NotBeNull();

        // Verify staff channel login now succeeds!
        var staffLogin = await authService.LoginAsync(new LoginRequest
        {
            UsernameOrEmail = "dynamic_user",
            Password = "DynamicPassword123!",
            LoginChannel = "staff"
        });
        staffLogin.Should().NotBeNull();
        staffLogin.User.Username.Should().Be("dynamic_user");

        // Verify customer channel login now fails!
        var customerLoginAttempt2 = async () => await authService.LoginAsync(new LoginRequest
        {
            UsernameOrEmail = "dynamic_user",
            Password = "DynamicPassword123!",
            LoginChannel = "customer"
        });
        await customerLoginAttempt2.Should().ThrowAsync<PortalAccessDeniedException>();
    }

    // ==========================================
    // PHASE 3: ADMIN DELETE USER TESTS
    // ==========================================

    [Fact]
    public async Task AdminDelete_NormalUser_Returns204AndRemovesUser()
    {
        var (dbContext, _, controller, _) = await CreateFixtureAsync();

        // Admin caller
        var adminId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, adminId.ToString()),
            new(ClaimTypes.Role, "ADMIN")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        // Create a normal customer user
        var customerRole = await dbContext.Roles.FirstAsync(r => r.Name == "CUSTOMER");
        var userToDelete = new User
        {
            Id = Guid.NewGuid(),
            Username = "user_to_delete",
            Email = "delete.me@fleetflow.io",
            PasswordHash = "hash"
        };
        userToDelete.Roles.Add(customerRole);
        dbContext.Users.Add(userToDelete);
        await dbContext.SaveChangesAsync();

        // Execute DELETE
        var result = await controller.DeleteUser(userToDelete.Id);

        var noContentResult = result as NoContentResult;
        noContentResult.Should().NotBeNull();
        noContentResult!.StatusCode.Should().Be(StatusCodes.Status204NoContent);

        // Verify user is removed from database
        var deletedUserInDb = await dbContext.Users.FindAsync(userToDelete.Id);
        deletedUserInDb.Should().BeNull();
    }

    [Fact]
    public async Task AdminDelete_NonExistentUser_Returns404()
    {
        var (_, _, controller, _) = await CreateFixtureAsync();

        var adminId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, adminId.ToString()),
            new(ClaimTypes.Role, "ADMIN")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        var missingId = Guid.NewGuid();
        var result = await controller.DeleteUser(missingId);

        var notFoundResult = result as NotFoundObjectResult;
        notFoundResult.Should().NotBeNull();
        notFoundResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task AdminDelete_Self_Returns400()
    {
        var (dbContext, _, controller, _) = await CreateFixtureAsync();

        var adminRole = await dbContext.Roles.FirstAsync(r => r.Name == "ADMIN");
        var adminId = Guid.NewGuid();
        var adminUser = new User
        {
            Id = adminId,
            Username = "acting_admin_self",
            Email = "acting.admin.self@fleetflow.io",
            PasswordHash = "hash"
        };
        adminUser.Roles.Add(adminRole);
        dbContext.Users.Add(adminUser);
        await dbContext.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, adminId.ToString()),
            new(ClaimTypes.Role, "ADMIN")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        var result = await controller.DeleteUser(adminId);

        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        // Verify user was NOT deleted
        var userInDb = await dbContext.Users.FindAsync(adminId);
        userInDb.Should().NotBeNull();
    }

    [Fact]
    public async Task AdminDelete_LastAdmin_Returns400()
    {
        var (dbContext, _, controller, _) = await CreateFixtureAsync();

        var adminRole = await dbContext.Roles.FirstAsync(r => r.Name == "ADMIN");
        var soleAdminId = Guid.NewGuid();
        var soleAdmin = new User
        {
            Id = soleAdminId,
            Username = "the_only_admin",
            Email = "sole.admin.delete@fleetflow.io",
            PasswordHash = "hash"
        };
        soleAdmin.Roles.Add(adminRole);
        dbContext.Users.Add(soleAdmin);
        await dbContext.SaveChangesAsync();

        // Another caller attempts to delete the sole admin
        var externalCallerId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, externalCallerId.ToString()),
            new(ClaimTypes.Role, "ADMIN")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        var result = await controller.DeleteUser(soleAdminId);

        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult.Should().NotBeNull();
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        // Verify admin was NOT deleted
        var adminInDb = await dbContext.Users.FindAsync(soleAdminId);
        adminInDb.Should().NotBeNull();
    }

    [Fact]
    public void AdminDelete_NonAdminRole_Returns403()
    {
        var method = typeof(AdminUsersController).GetMethod(nameof(AdminUsersController.DeleteUser));
        method.Should().NotBeNull();

        var methodAuth = method!.GetCustomAttribute<AuthorizeAttribute>();
        methodAuth.Should().NotBeNull();
        methodAuth!.Roles.Should().Be("ADMIN");
        methodAuth.Roles.Should().NotContain("CUSTOMER");
        methodAuth.Roles.Should().NotContain("FLEET_MANAGER");
        methodAuth.Roles.Should().NotContain("MAINTENANCE_STAFF");
    }

    [Fact]
    public void AdminDelete_Unauthenticated_Returns401()
    {
        var method = typeof(AdminUsersController).GetMethod(nameof(AdminUsersController.DeleteUser));
        method.Should().NotBeNull();

        var allowAnonymous = method!.GetCustomAttribute<AllowAnonymousAttribute>();
        allowAnonymous.Should().BeNull();

        var classAuth = typeof(AdminUsersController).GetCustomAttribute<AuthorizeAttribute>();
        var methodAuth = method.GetCustomAttribute<AuthorizeAttribute>();
        (classAuth != null || methodAuth != null).Should().BeTrue();
    }

    [Fact]
    public async Task AdminDelete_RemovesUserRoleMapping()
    {
        var (dbContext, _, controller, _) = await CreateFixtureAsync();

        var adminId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, adminId.ToString()),
            new(ClaimTypes.Role, "ADMIN")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        var role1 = await dbContext.Roles.FirstAsync(r => r.Name == "FLEET_MANAGER");
        var role2 = await dbContext.Roles.FirstAsync(r => r.Name == "MAINTENANCE_STAFF");
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "multi_role_delete",
            Email = "multirole@fleetflow.io",
            PasswordHash = "hash"
        };
        user.Roles.Add(role1);
        user.Roles.Add(role2);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Execute delete
        var result = await controller.DeleteUser(user.Id);
        var noContentResult = result as NoContentResult;
        noContentResult.Should().NotBeNull();
        noContentResult!.StatusCode.Should().Be(StatusCodes.Status204NoContent);

        // Verify User is gone
        var userInDb = await dbContext.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == user.Id);
        userInDb.Should().BeNull();
    }

    [Fact]
    public async Task AdminDelete_DoesNotAffectOtherUsers()
    {
        var (dbContext, _, controller, _) = await CreateFixtureAsync();

        var adminId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, adminId.ToString()),
            new(ClaimTypes.Role, "ADMIN")
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        var customerRole = await dbContext.Roles.FirstAsync(r => r.Name == "CUSTOMER");
        var userA = new User
        {
            Id = Guid.NewGuid(),
            Username = "user_a",
            Email = "usera@fleetflow.io",
            FullName = "User Alpha",
            PasswordHash = "hashA"
        };
        userA.Roles.Add(customerRole);

        var userB = new User
        {
            Id = Guid.NewGuid(),
            Username = "user_b",
            Email = "userb@fleetflow.io",
            FullName = "User Beta",
            PasswordHash = "hashB"
        };
        userB.Roles.Add(customerRole);

        dbContext.Users.AddRange(userA, userB);
        await dbContext.SaveChangesAsync();

        // Delete User A
        var result = await controller.DeleteUser(userA.Id);
        var noContentResult = result as NoContentResult;
        noContentResult.Should().NotBeNull();

        // Verify User A deleted
        var userAInDb = await dbContext.Users.FindAsync(userA.Id);
        userAInDb.Should().BeNull();

        // Verify User B untouched
        var userBInDb = await dbContext.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == userB.Id);
        userBInDb.Should().NotBeNull();
        userBInDb!.Username.Should().Be("user_b");
        userBInDb.Email.Should().Be("userb@fleetflow.io");
        userBInDb.FullName.Should().Be("User Beta");
        userBInDb.Roles.Should().ContainSingle(r => r.Name == "CUSTOMER");
    }
}


