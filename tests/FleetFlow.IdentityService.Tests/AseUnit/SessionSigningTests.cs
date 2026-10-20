using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IdentityService.Api.Data;
using IdentityService.Api.Entities;
using IdentityService.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace FleetFlow.IdentityService.Tests.AseUnit;

[Trait("Category", "ASE.Unit")]
[Trait("Owner", "Sanjeevsaran")]
public sealed class SessionSigningTests
{
    private const string Key = "synthetic-unit-signing-key-32-bytes-minimum";

    // No EF provider is configured: any accidental database access fails instead of reaching a server.
    private static IdentityDbContext NoDatabase() => new(new DbContextOptionsBuilder<IdentityDbContext>().Options);
    private static IConfiguration Settings(string? key, string? minutes = "15") => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["Jwt:Key"] = key, ["Jwt:Issuer"] = "unit-issuer", ["Jwt:Audience"] = "unit-audience", ["Jwt:ExpiryInMinutes"] = minutes }).Build();

    private static User Account(bool forced = false) => new()
    {
        Id = Guid.Parse("33333333-3333-4333-8333-333333333333"), FullName = "Synthetic Driver", Username = "unit_driver",
        Email = "driver@example.invalid", PhoneNumber = "+94770000000", Address = "Synthetic address",
        DrivingLicenseNumber = "SYNTHETIC-ONLY", ProfileImageUrl = "/uploads/synthetic.webp", CreatedAt = DateTime.UnixEpoch,
        AccessVersion = 7, MustChangePassword = forced, Roles = [new() { Id = Guid.Parse("44444444-4444-4444-8444-444444444444"), Name = "CUSTOMER" }]
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CreateSession_SignsClaimsAndPreservesRevocationAndPasswordFlags(bool forced)
    {
        using var db = NoDatabase();
        var hasher = new Mock<IPasswordHasher<User>>(MockBehavior.Strict);
        var subject = new AuthenticationService(db, hasher.Object, Settings(Key));
        var account = Account(forced);
        var before = DateTime.UtcNow;

        var response = subject.CreateSession(account);

        var after = DateTime.UtcNow;
        var principal = new JwtSecurityTokenHandler().ValidateToken(response.Token, new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = "unit-issuer", ValidateAudience = true, ValidAudience = "unit-audience",
            ValidateLifetime = true, ClockSkew = TimeSpan.Zero, ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        }, out var validated);
        Assert.Equal(SecurityAlgorithms.HmacSha256, Assert.IsType<JwtSecurityToken>(validated).Header.Alg);
        Assert.Equal(account.Id.ToString(), principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal(account.Username, principal.FindFirstValue(ClaimTypes.Name));
        Assert.Equal(account.Email, principal.FindFirstValue(ClaimTypes.Email));
        Assert.Equal("7", principal.FindFirstValue("access_version"));
        Assert.Equal(forced ? "true" : "false", principal.FindFirstValue("must_change_password"));
        Assert.Equal(new[] { "CUSTOMER" }, principal.FindAll(ClaimTypes.Role).Select(c => c.Value));
        Assert.True(Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Jti), out var id) && id != Guid.Empty);
        Assert.InRange(response.Expiration, before.AddMinutes(15), after.AddMinutes(15));
        Assert.Equal(forced, response.MustChangePassword);
        Assert.Equal(account.Id, response.User.Id);
        Assert.Equal(account.FullName, response.User.FullName);
        Assert.Equal(account.ProfileImageUrl, response.User.ProfileImageUrl);
        Assert.Equal(account.CreatedAt, response.User.CreatedAt);
        hasher.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("short")]
    [InlineData("1234567890123456789012345678901")]
    public void CreateSession_WeakOrMissingKeyNeverProducesToken(string? key)
    {
        using var db = NoDatabase();
        var hasher = new Mock<IPasswordHasher<User>>(MockBehavior.Strict);
        var subject = new AuthenticationService(db, hasher.Object, Settings(key));
        var error = Assert.Throws<InvalidOperationException>(() => subject.CreateSession(Account()));
        Assert.Contains("32 bytes", error.Message);
        hasher.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("12345678901234567890123456789012")]
    [InlineData("éééééééééééééééé")]
    public void CreateSession_MinimumKeyLengthIsUtf8BytesRatherThanCharacterCount(string key)
    {
        using var db = NoDatabase();
        var subject = new AuthenticationService(db, new Mock<IPasswordHasher<User>>(MockBehavior.Strict).Object, Settings(key));
        Assert.Equal(32, Encoding.UTF8.GetByteCount(key));
        Assert.Equal(SecurityAlgorithms.HmacSha256, new JwtSecurityTokenHandler().ReadJwtToken(subject.CreateSession(Account()).Token).Header.Alg);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-number")]
    public void CreateSession_InvalidExpiryUsesTheDocumentedSixtyMinuteDefault(string? minutes)
    {
        using var db = NoDatabase();
        var subject = new AuthenticationService(db, new Mock<IPasswordHasher<User>>(MockBehavior.Strict).Object, Settings(Key, minutes));
        var before = DateTime.UtcNow;
        var response = subject.CreateSession(Account());
        Assert.InRange(response.Expiration, before.AddMinutes(60), DateTime.UtcNow.AddMinutes(60));
    }
}
