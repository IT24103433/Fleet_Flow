using System.Security.Claims;
using System.Text.Json;
using IdentityService.Api.Controllers;
using IdentityService.Api.Dtos;
using IdentityService.Api.Exceptions;
using IdentityService.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FleetFlow.IdentityService.Tests.AseUnit;

[Trait("Category", "ASE.Unit")]
[Trait("Owner", "Sanjeevsaran")]
public sealed class AuthenticationBoundaryTests
{
    private static readonly Guid CustomerId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private readonly Mock<IRegistrationService> registration = new(MockBehavior.Strict);
    private readonly Mock<IAuthenticationService> authentication = new(MockBehavior.Strict);
    private readonly Mock<IProfileImageService> photos = new(MockBehavior.Strict);

    private AuthController Controller(string? subject = null, bool forcedPassword = false)
    {
        var claims = new List<Claim>();
        if (subject != null) claims.Add(new(ClaimTypes.NameIdentifier, subject));
        if (forcedPassword) claims.Add(new("must_change_password", "true"));
        return new(registration.Object, authentication.Object, photos.Object)
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext
            { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "unit-test")) } }
        };
    }

    private void NoUnexpectedCalls()
    {
        authentication.VerifyNoOtherCalls();
        registration.VerifyNoOtherCalls();
        photos.VerifyNoOtherCalls();
    }

    public static TheoryData<Exception, int, string> LoginFailures => new()
    {
        { new InvalidCredentialsException("Invalid credentials."), 401, "Invalid credentials." },
        { new AccountDisabledException("Disabled."), 403, "Disabled." },
        { new PortalAccessDeniedException("Use staff portal."), 403, "Use staff portal." },
        { new TimeoutException("private database host must never escape"), 500,
            "Sign-in is temporarily unavailable. Please try again." }
    };

    [Theory]
    [MemberData(nameof(LoginFailures))]
    public async Task Login_DependencyFailure_MapsStatusWithoutLeakingInfrastructure(
        Exception failure, int expectedStatus, string expectedMessage)
    {
        var request = new LoginRequest { UsernameOrEmail = "synthetic-user", Password = "unit-input", LoginChannel = "customer" };
        authentication.Setup(s => s.LoginAsync(request)).ThrowsAsync(failure);

        var result = Assert.IsAssignableFrom<ObjectResult>((await Controller().Login(request)).Result);

        Assert.Equal(expectedStatus, result.StatusCode);
        Assert.Equal(expectedMessage, JsonSerializer.SerializeToElement(result.Value).GetProperty("message").GetString());
        Assert.DoesNotContain("private database host", JsonSerializer.Serialize(result.Value));
        authentication.Verify(s => s.LoginAsync(request), Times.Once);
        NoUnexpectedCalls();
    }

    [Theory]
    [InlineData("customer")]
    [InlineData("staff")]
    public async Task Login_Success_ForwardsExactRequestAndPreservesSession(string channel)
    {
        var request = new LoginRequest { UsernameOrEmail = "synthetic", Password = "unit-input", LoginChannel = channel };
        var session = new LoginResponse { Token = "opaque-unit-session", User = new UserResponse { Id = CustomerId } };
        authentication.Setup(s => s.LoginAsync(request)).ReturnsAsync(session);

        var result = Assert.IsType<OkObjectResult>((await Controller().Login(request)).Result);

        Assert.Same(session, result.Value);
        authentication.Verify(s => s.LoginAsync(request), Times.Once);
        NoUnexpectedCalls();
    }

    [Fact]
    public async Task Registration_Duplicate_IsConflictAndNeverStartsLogin()
    {
        var request = new RegisterRequest();
        registration.Setup(s => s.RegisterAsync(request)).ThrowsAsync(new DuplicateException("Identifier already owned."));
        var result = Assert.IsType<ConflictObjectResult>((await Controller().Register(request)).Result);
        Assert.Equal("Identifier already owned.", JsonSerializer.SerializeToElement(result.Value).GetProperty("message").GetString());
        registration.Verify(s => s.RegisterAsync(request), Times.Once);
        NoUnexpectedCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task InvalidSubject_DeniesProfileAndPasswordActionsBeforeCallingDependencies(string? subject)
    {
        var controller = Controller(subject);
        var file = new Mock<IFormFile>(MockBehavior.Strict).Object;
        Assert.IsType<UnauthorizedObjectResult>(await controller.UploadProfilePicture(file));
        Assert.IsType<UnauthorizedObjectResult>(await controller.GetProfilePicture());
        Assert.IsType<UnauthorizedObjectResult>(await controller.DeleteProfilePicture());
        Assert.IsType<UnauthorizedObjectResult>(await controller.ChangePassword(new()));
        NoUnexpectedCalls();
    }

    [Fact]
    public async Task MissingPhoto_IsBadRequestWithoutUsingStorage()
    {
        Assert.IsType<BadRequestObjectResult>(await Controller(CustomerId.ToString()).UploadProfilePicture(null!));
        NoUnexpectedCalls();
    }

    [Fact]
    public async Task PhotoUpload_UsesAuthenticatedOwnerAndExactFile()
    {
        var file = new Mock<IFormFile>(MockBehavior.Strict).Object;
        photos.Setup(s => s.UploadProfilePictureAsync(CustomerId, file)).ReturnsAsync("/uploads/synthetic.webp");
        var result = Assert.IsType<OkObjectResult>(await Controller(CustomerId.ToString()).UploadProfilePicture(file));
        Assert.Equal("/uploads/synthetic.webp", JsonSerializer.SerializeToElement(result.Value).GetProperty("profileImageUrl").GetString());
        photos.Verify(s => s.UploadProfilePictureAsync(CustomerId, file), Times.Once);
        NoUnexpectedCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PasswordChange_RenewsOnlyForcedPasswordSession(bool forced)
    {
        var request = new ChangePasswordRequest { NewPassword = "Synthetic-new-input" };
        var renewed = new LoginResponse { Token = "opaque-renewed-session", User = new UserResponse { Id = CustomerId } };
        var sequence = new MockSequence();
        authentication.InSequence(sequence).Setup(s => s.ChangePasswordAsync(CustomerId, request)).Returns(Task.CompletedTask);
        if (forced) authentication.InSequence(sequence).Setup(s => s.RenewSessionAsync(CustomerId)).ReturnsAsync(renewed);

        var result = Assert.IsType<OkObjectResult>(await Controller(CustomerId.ToString(), forced).ChangePassword(request));
        var body = JsonSerializer.SerializeToElement(result.Value);

        Assert.Equal("Password changed successfully.", body.GetProperty("message").GetString());
        Assert.Equal(forced ? renewed.Token : null, body.GetProperty("token").GetString());
        authentication.Verify(s => s.ChangePasswordAsync(CustomerId, request), Times.Once);
        authentication.Verify(s => s.RenewSessionAsync(CustomerId), forced ? Times.Once() : Times.Never());
        NoUnexpectedCalls();
    }

    public static TheoryData<Exception, int> PasswordFailures => new()
    {
        { new NotFoundException("Unknown user."), 404 },
        { new AccountDisabledException("Disabled user."), 403 },
        { new ArgumentException("Current password mismatch."), 400 }
    };

    [Theory]
    [MemberData(nameof(PasswordFailures))]
    public async Task PasswordChange_FailureNeverRenewsSession(Exception failure, int status)
    {
        var request = new ChangePasswordRequest { NewPassword = "Synthetic-new-input" };
        authentication.Setup(s => s.ChangePasswordAsync(CustomerId, request)).ThrowsAsync(failure);
        var result = Assert.IsAssignableFrom<ObjectResult>(await Controller(CustomerId.ToString(), true).ChangePassword(request));
        Assert.Equal(status, result.StatusCode);
        authentication.Verify(s => s.ChangePasswordAsync(CustomerId, request), Times.Once);
        NoUnexpectedCalls();
    }

    [Fact]
    public async Task InvalidModel_NeverChangesPassword()
    {
        var controller = Controller(CustomerId.ToString(), true);
        controller.ModelState.AddModelError("NewPassword", "Too short.");
        Assert.IsType<BadRequestObjectResult>(await controller.ChangePassword(new()));
        NoUnexpectedCalls();
    }
}
