using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FleetFlow.Contracts;
using FleetFlow.ServiceDefaults;
using IdentityService.Api.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using ServiceDatabaseTool;

namespace FleetFlow.ServiceFoundation.Tests;

public sealed class SecurityTests
{
    internal const string UserKey = "foundation-test-user-signing-key-123456789";
    internal const string InternalKey = "foundation-test-internal-key-1234567890123";
    internal const string Secret = "foundation-test-client-secret-12345678901";
    internal static ServiceAuthenticationOptions Options(bool enabled = true) => new(enabled, InternalKey,
        "FleetFlow.IdentityService", ServiceScopes.ClientGrants.Keys.ToDictionary(id => id, _ => Secret));
    internal static IConfiguration Config(params (string Key, string Value)[] entries) =>
        new ConfigurationBuilder().AddInMemoryCollection(entries.Select(e => new KeyValuePair<string,string?>(e.Key,e.Value))).Build();

    [Theory]
    [InlineData("rental-service")][InlineData("fleet-service")][InlineData("notification-service")]
    public void IssuedTokenHasOnlyFixedClientScopesAndFiveMinuteLifetime(string client)
    {
        var options = Options();
        var response = new ServiceTokenIssuer(options, TimeProvider.System).Issue(new(client, Secret));
        Assert.NotNull(response);
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(response.AccessToken, options.ValidationParameters(), out var validated);
        var jwt = Assert.IsType<JwtSecurityToken>(validated);
        Assert.Equal(ServiceScopes.ClientGrants[client].Order(), principal.FindAll("scope").Select(c => c.Value).Order());
        Assert.Equal(client, principal.FindFirst("sub")?.Value);
        Assert.Empty(principal.FindAll(ClaimTypes.Role));
        Assert.Equal(TimeSpan.FromSeconds(300), jwt.ValidTo-jwt.ValidFrom);
        Assert.Equal(300, response.ExpiresIn);
    }

    [Theory]
    [InlineData("unknown",Secret,true)] [InlineData("rental-service","wrong",true)] [InlineData("rental-service",Secret,false)]
    public void UnknownInvalidOrDisabledCredentialsNeverGetToken(string client,string secret,bool enabled) =>
        Assert.Null(new ServiceTokenIssuer(Options(enabled),TimeProvider.System).Issue(new(client,secret)));

    [Theory]
    [InlineData("short")] [InlineData(UserKey)]
    public void EnablingServiceAuthRequiresIndependentStrongKey(string key) =>
        Assert.Throws<InvalidOperationException>(()=>ServiceAuthenticationOptions.Read(Config(
            ("SERVICE_AUTH_ENABLED","true"),("SERVICE_AUTH_SIGNING_KEY",key)),UserKey));

    [Fact]
    public void EnvironmentDisableOverridesConfiguredEnable() =>
        Assert.False(ServiceAuthenticationOptions.Read(Config(("ServiceAuth:Enabled","true"),("SERVICE_AUTH_ENABLED","false")),UserKey).Enabled);

    [Theory]
    [InlineData("FleetFlow.Client",false,SecurityAlgorithms.HmacSha256)]
    [InlineData("FleetFlow.Internal",true,SecurityAlgorithms.HmacSha256)]
    [InlineData("FleetFlow.Internal",false,SecurityAlgorithms.HmacSha384)]
    public void WrongAudienceExpiredOrWrongAlgorithmRejected(string audience,bool expired,string algorithm)
    {
        var now=DateTime.UtcNow;
        var jwt=new JwtSecurityToken("FleetFlow.IdentityService",audience,[],now.AddMinutes(-10),
            expired?now.AddMinutes(-1):now.AddMinutes(1),new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(InternalKey+InternalKey)),algorithm));
        var parameters=Options().ValidationParameters();
        parameters.IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(InternalKey+InternalKey));
        Assert.ThrowsAny<SecurityTokenException>(()=>new JwtSecurityTokenHandler().ValidateToken(
            new JwtSecurityTokenHandler().WriteToken(jwt),parameters,out _));
    }

    [Theory]
    [InlineData("rental","fleetflow_fleet")][InlineData("notifications","fleetflow_auth")]
    [InlineData("rental","fleetflow_rental;DROP DATABASE x")]
    public void MigrationRefusesForeignOrUnsafeDatabase(string service,string database) =>
        Assert.Throws<InvalidOperationException>(()=>FoundationMigration.ValidateDatabase(service,database));

    [Fact]
    public void OwnedConnectionCannotPointAtLegacyDatabase() =>
        Assert.Throws<InvalidOperationException>(()=>OwnedDatabase.Read(Config(("ConnectionStrings:DefaultConnection",
            "Host=localhost;Database=fleetflow_fleet;Username=test")),FoundationMigration.Descriptor("rental")));

    [Theory]
    [InlineData("http://example.com")][InlineData("https://user:secret@example.com")]
    [InlineData("https://example.com/path")][InlineData("https://example.com/?key=value")]
    public void IdentityUrlRejectsCredentialLeakTargets(string url) =>
        Assert.Throws<InvalidOperationException>(()=>ServiceEndpoints.Identity(Config(("INTERNAL_IDENTITY_URL",url)),new TestEnvironment()));

    [Fact]
    public void ProductionIdentityBrokerRequiresHttpsAndNeverCachesResponse()
    {
        var controller=new ServiceTokensController(new ServiceTokenIssuer(Options(),TimeProvider.System),Options(),new TestEnvironment { EnvironmentName="Production" });
        controller.ControllerContext=new ControllerContext { HttpContext=new DefaultHttpContext() };
        Assert.IsType<BadRequestObjectResult>(controller.Create(new("rental-service",Secret)).Result);
        Assert.Equal("no-store",controller.Response.Headers.CacheControl);
        controller.Request.Scheme="https";
        Assert.IsType<OkObjectResult>(controller.Create(new("rental-service",Secret)).Result);
    }

    [Fact]
    public void BrokerRejectsInvalidCredentialsAndDisabledServiceAuthentication()
    {
        foreach(var enabled in new[]{true,false})
        {
            var options=Options(enabled);
            var controller=new ServiceTokensController(new ServiceTokenIssuer(options,TimeProvider.System),options,new TestEnvironment());
            controller.ControllerContext=new ControllerContext { HttpContext=new DefaultHttpContext() };
            var result=Assert.IsAssignableFrom<ObjectResult>(controller.Create(new("rental-service","incorrect")).Result);
            Assert.Equal(enabled?401:503,result.StatusCode);
            Assert.Equal("no-store",controller.Response.Headers.CacheControl);
        }
    }
}

internal sealed class TestEnvironment : IWebHostEnvironment
{
    public string EnvironmentName { get; set; }="Development";
    public string ApplicationName { get; set; }="Tests";
    public string ContentRootPath { get; set; }=".";
    public IFileProvider ContentRootFileProvider { get; set; }=new NullFileProvider();
    public string WebRootPath { get; set; }=".";
    public IFileProvider WebRootFileProvider { get; set; }=new NullFileProvider();
}
