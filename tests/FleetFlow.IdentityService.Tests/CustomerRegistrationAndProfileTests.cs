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
using Xunit;

namespace FleetFlow.IdentityService.Tests;

public class CustomerRegistrationAndProfileTests
{
    private readonly IConfiguration _configuration;
    private readonly IPasswordHasher<User> _passwordHasher;

    public CustomerRegistrationAndProfileTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:Issuer", "FleetFlowIdentityService" },
                { "Jwt:Audience", "FleetFlowClients" },
                { "Jwt:Key", "SuperSecretKeyForCustomerProfileTestsOnly1234567890!" },
                { "Jwt:ExpiryInMinutes", "60" }
            })
            .Build();

        _passwordHasher = new PasswordHasher<User>();
    }

    private (IdentityDbContext dbContext, RegistrationService regService, AuthenticationService authService) CreateServices()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var dbContext = new IdentityDbContext(options);
        var regService = new RegistrationService(dbContext, _passwordHasher);
        var authService = new AuthenticationService(dbContext, _passwordHasher, _configuration);

        return (dbContext, regService, authService);
    }

    private static RegisterRequest CreateCustomerRegisterRequest(
        string fullName = "Alex Morgan",
        string username = "alex_morgan",
        string email = "alex.morgan@fleetflow.io",
        string phoneNumber = "+1 555 234 5678",
        string address = "100 Mobility Way, Metro City",
        string drivingLicenseNumber = "DL-8492048-A",
        string password = "SecurePassword123!") => new()
    {
        FullName = fullName,
        Username = username,
        Email = email,
        PhoneNumber = phoneNumber,
        Address = address,
        DrivingLicenseNumber = drivingLicenseNumber,
        Password = password,
        ConfirmPassword = password
    };

    // 1. Registration with all valid fields succeeds
    [Fact]
    public async Task TC_REG_01_RegistrationWithAllValidFields_Succeeds()
    {
        var (dbContext, regService, _) = CreateServices();
        var request = CreateCustomerRegisterRequest();

        var response = await regService.RegisterAsync(request);

        response.Should().NotBeNull();
        response.Id.Should().NotBe(Guid.Empty);
        response.Username.Should().Be(request.Username);
        response.Email.Should().Be(request.Email);
    }

    // 2. Full name is persisted correctly
    [Fact]
    public async Task TC_REG_02_FullName_PersistedCorrectly()
    {
        var (dbContext, regService, _) = CreateServices();
        var request = CreateCustomerRegisterRequest(fullName: "Marcus Aurelius");

        var response = await regService.RegisterAsync(request);

        response.FullName.Should().Be("Marcus Aurelius");
        var savedUser = await dbContext.Users.FindAsync(response.Id);
        savedUser.Should().NotBeNull();
        savedUser!.FullName.Should().Be("Marcus Aurelius");
    }

    // 3. Phone number is persisted correctly
    [Fact]
    public async Task TC_REG_03_PhoneNumber_PersistedCorrectly()
    {
        var (dbContext, regService, _) = CreateServices();
        var request = CreateCustomerRegisterRequest(phoneNumber: "+94 77 987 6543");

        var response = await regService.RegisterAsync(request);

        response.PhoneNumber.Should().Be("+94 77 987 6543");
        var savedUser = await dbContext.Users.FindAsync(response.Id);
        savedUser.Should().NotBeNull();
        savedUser!.PhoneNumber.Should().Be("+94 77 987 6543");
    }

    // 4. Address is persisted correctly
    [Fact]
    public async Task TC_REG_04_Address_PersistedCorrectly()
    {
        var (dbContext, regService, _) = CreateServices();
        var request = CreateCustomerRegisterRequest(address: "742 Evergreen Terrace, Springfield");

        var response = await regService.RegisterAsync(request);

        response.Address.Should().Be("742 Evergreen Terrace, Springfield");
        var savedUser = await dbContext.Users.FindAsync(response.Id);
        savedUser.Should().NotBeNull();
        savedUser!.Address.Should().Be("742 Evergreen Terrace, Springfield");
    }

    // 5. Driving license number is persisted correctly
    [Fact]
    public async Task TC_REG_05_DrivingLicenseNumber_PersistedCorrectly()
    {
        var (dbContext, regService, _) = CreateServices();
        var request = CreateCustomerRegisterRequest(drivingLicenseNumber: "DL-99887766-B");

        var response = await regService.RegisterAsync(request);

        response.DrivingLicenseNumber.Should().Be("DL-99887766-B");
        var savedUser = await dbContext.Users.FindAsync(response.Id);
        savedUser.Should().NotBeNull();
        savedUser!.DrivingLicenseNumber.Should().Be("DL-99887766-B");
    }

    // 6. Username/email duplicate behavior still works
    [Fact]
    public async Task TC_REG_06_DuplicateUsernameAndEmail_RejectedWithDuplicateException()
    {
        var (dbContext, regService, _) = CreateServices();
        var request1 = CreateCustomerRegisterRequest(username: "unique_user", email: "unique@fleetflow.io");
        await regService.RegisterAsync(request1);

        // Duplicate username
        var duplicateUsernameRequest = CreateCustomerRegisterRequest(username: "UNIQUE_USER", email: "different@fleetflow.io");
        var actUsername = () => regService.RegisterAsync(duplicateUsernameRequest);
        await actUsername.Should().ThrowAsync<DuplicateException>()
            .WithMessage("*Username is already taken*");

        // Duplicate email
        var duplicateEmailRequest = CreateCustomerRegisterRequest(username: "different_user", email: "UNIQUE@FLEETFLOW.IO");
        var actEmail = () => regService.RegisterAsync(duplicateEmailRequest);
        await actEmail.Should().ThrowAsync<DuplicateException>()
            .WithMessage("*Email is already registered*");
    }

    // 7. Password is not stored as plaintext
    [Fact]
    public async Task TC_REG_07_Password_NotStoredAsPlaintext()
    {
        var (dbContext, regService, _) = CreateServices();
        const string rawPassword = "SuperSecretPassword123!";
        var request = CreateCustomerRegisterRequest(password: rawPassword);

        var response = await regService.RegisterAsync(request);

        var savedUser = await dbContext.Users.FindAsync(response.Id);
        savedUser.Should().NotBeNull();
        savedUser!.PasswordHash.Should().NotBe(rawPassword);
        savedUser.PasswordHash.Should().NotBeNullOrWhiteSpace();

        var verifyResult = _passwordHasher.VerifyHashedPassword(savedUser, savedUser.PasswordHash, rawPassword);
        verifyResult.Should().Be(PasswordVerificationResult.Success);
    }

    // 8. Invalid / missing required fields are rejected
    [Theory]
    [InlineData("", "alex", "alex@test.com", "12345678", "123 Main St", "DL-1234", "FullName")]
    [InlineData("Alex", "alex", "alex@test.com", "", "123 Main St", "DL-1234", "PhoneNumber")]
    [InlineData("Alex", "alex", "alex@test.com", "12345678", "", "DL-1234", "Address")]
    [InlineData("Alex", "alex", "alex@test.com", "12345678", "123 Main St", "", "DrivingLicenseNumber")]
    public async Task TC_REG_08_MissingRequiredFields_Rejected(
        string fullName, string username, string email, string phone, string address, string license, string expectedParam)
    {
        var (_, regService, _) = CreateServices();
        var request = CreateCustomerRegisterRequest(
            fullName: fullName,
            username: username,
            email: email,
            phoneNumber: phone,
            address: address,
            drivingLicenseNumber: license
        );

        var act = () => regService.RegisterAsync(request);
        var ex = await act.Should().ThrowAsync<ArgumentException>();
        ex.Which.ParamName.Should().Be(expectedParam);
    }

    // 9. Customer login still works after registration
    [Fact]
    public async Task TC_REG_09_CustomerLoginWorksAfterRegistration()
    {
        var (_, regService, authService) = CreateServices();
        var regRequest = CreateCustomerRegisterRequest(
            username: "fresh_customer",
            password: "SecurePassword123!"
        );
        await regService.RegisterAsync(regRequest);

        var loginRequest = new LoginRequest
        {
            UsernameOrEmail = "fresh_customer",
            Password = "SecurePassword123!",
            LoginChannel = "customer"
        };

        var loginResponse = await authService.LoginAsync(loginRequest);

        loginResponse.Should().NotBeNull();
        loginResponse.Token.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(loginResponse.Token);
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "CUSTOMER");
    }

    // 10. Login returns/accesses the correct customer profile data
    [Fact]
    public async Task TC_REG_10_LoginReturnsPersistedCustomerProfileData()
    {
        var (_, regService, authService) = CreateServices();
        var regRequest = CreateCustomerRegisterRequest(
            fullName: "Sarah Connor",
            username: "sarah_connor",
            email: "sarah@cyberdyne.com",
            phoneNumber: "+1 800 555 0199",
            address: "Cyberdyne Systems Bldg 4, Los Angeles",
            drivingLicenseNumber: "CA-TERMINATOR-01"
        );
        await regService.RegisterAsync(regRequest);

        var loginRequest = new LoginRequest
        {
            UsernameOrEmail = "sarah@cyberdyne.com",
            Password = "SecurePassword123!",
            LoginChannel = "customer"
        };

        var loginResponse = await authService.LoginAsync(loginRequest);

        loginResponse.User.Should().NotBeNull();
        loginResponse.User.FullName.Should().Be("Sarah Connor");
        loginResponse.User.Username.Should().Be("sarah_connor");
        loginResponse.User.Email.Should().Be("sarah@cyberdyne.com");
        loginResponse.User.PhoneNumber.Should().Be("+1 800 555 0199");
        loginResponse.User.Address.Should().Be("Cyberdyne Systems Bldg 4, Los Angeles");
        loginResponse.User.DrivingLicenseNumber.Should().Be("CA-TERMINATOR-01");
    }

    // IMPORTANT SECURITY TEST: Verify responses do NOT expose password hashes or sensitive internal Identity data
    [Fact]
    public async Task TC_SEC_01_ResponsesDoNotExposePasswordHashOrCredentials()
    {
        var (_, regService, authService) = CreateServices();
        var regRequest = CreateCustomerRegisterRequest();

        var regResponse = await regService.RegisterAsync(regRequest);
        var loginResponse = await authService.LoginAsync(new LoginRequest
        {
            UsernameOrEmail = regRequest.Username,
            Password = regRequest.Password,
            LoginChannel = "customer"
        });

        // Reflection check: Ensure UserResponse has no password or hash properties
        var userResponseProps = typeof(UserResponse).GetProperties().Select(p => p.Name).ToList();
        userResponseProps.Should().NotContain("Password");
        userResponseProps.Should().NotContain("PasswordHash");
        userResponseProps.Should().NotContain("Salt");
        userResponseProps.Should().NotContain("SecurityStamp");

        // Object check on registration response
        regResponse.GetType().GetProperty("Password").Should().BeNull();
        regResponse.GetType().GetProperty("PasswordHash").Should().BeNull();

        // Object check on login response
        loginResponse.GetType().GetProperty("Password").Should().BeNull();
        loginResponse.GetType().GetProperty("PasswordHash").Should().BeNull();
        loginResponse.User.GetType().GetProperty("Password").Should().BeNull();
        loginResponse.User.GetType().GetProperty("PasswordHash").Should().BeNull();
    }
}
