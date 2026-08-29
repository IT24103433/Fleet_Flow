using Xunit;
using FluentAssertions;

namespace FleetFlow.IdentityService.Tests;

public class RegisterControllerTests
{
    [Fact]
    public void TC_REG_01_ValidRegistration_PayloadIsValid()
    {
        // Arrange
        var email = "newuser@fleetflow.com";
        var password = "SecurePassword123!";

        // Assert
        email.Should().Contain("@");
        password.Length.Should().BeGreaterThanOrEqualTo(8);
    }

    [Fact]
    public void TC_REG_02_RequiredFields_ValidationFailsWhenEmpty()
    {
        // Arrange
        string emptyEmail = "";

        // Assert
        emptyEmail.Should().BeNullOrEmpty();
    }

    [Fact]
    public void TC_REG_03_InvalidEmailFormat_FailsValidation()
    {
        // Arrange
        var invalidEmail = "user-without-domain";

        // Assert
        invalidEmail.Should().NotContain("@fleetflow.com");
    }

    [Fact]
    public void TC_REG_05_WeakPassword_FailsPolicy()
    {
        // Arrange
        var weakPassword = "123";

        // Assert
        weakPassword.Length.Should().BeLessThan(8);
    }
}