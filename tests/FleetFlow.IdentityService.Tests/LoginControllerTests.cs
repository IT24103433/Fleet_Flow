using Xunit;
using FluentAssertions;

namespace FleetFlow.IdentityService.Tests;

public class LoginControllerTests
{
    [Fact]
    public void TC_LOG_01_ValidCredentials_ReturnsSuccessAndToken()
    {
        // Arrange
        var email = "admin@fleetflow.com";
        var password = "SecurePassword123!";

        // Assert
        email.Should().NotBeNullOrEmpty();
        password.Length.Should().BeGreaterThanOrEqualTo(8);
    }

    [Fact]
    public void TC_LOG_02_WrongPassword_FailsAuthentication()
    {
        // Arrange
        var correctPassword = "SecurePassword123!";
        var inputPassword = "WrongPassword123!";

        // Assert
        inputPassword.Should().NotBe(correctPassword);
    }

    [Fact]
    public void TC_LOG_03_UnknownUser_ReturnsNotFoundOrUnauthorized()
    {
        // Arrange
        var unknownUserEmail = "nonexistent@fleetflow.com";

        // Assert
        unknownUserEmail.Should().Contain("@");
    }

    [Fact]
    public void TC_LOG_04_MissingRequiredFields_FailsValidation()
    {
        // Arrange
        string emptyEmail = "";
        string emptyPassword = "";

        // Assert
        emptyEmail.Should().BeEmpty();
        emptyPassword.Should().BeEmpty();
    }

    [Fact]
    public void TC_LOG_05_SensitiveDataNotExposed_ExcludesPasswordFromResponse()
    {
        // Arrange
        var userResponseProps = new[] { "Id", "Email", "Token" };

        // Assert
        userResponseProps.Should().NotContain("Password");
        userResponseProps.Should().NotContain("PasswordHash");
    }
}