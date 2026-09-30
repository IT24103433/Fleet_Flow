using FleetService.Api.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace FleetService.Tests;

public class JwtSigningKeyTests
{
    private const string DevelopmentKey = "FleetFlowSuperSecretSecurityKey2026!#ForJWTTokenGeneration";
    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Theory]
    [InlineData("Production", null)]
    [InlineData("Staging", null)]
    [InlineData("Production", "")]
    [InlineData("Staging", "   ")]
    [InlineData("Production", "short")]
    [InlineData("Staging", DevelopmentKey)]
    [InlineData("Production", DevelopmentKey)]
    [InlineData("Development", null)]
    public void MissingWeakOrProductionDevelopmentKeys_FailClearly(string environment, string? key)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = key }).Build();
        var error = Assert.Throws<InvalidOperationException>(() => JwtSigningKey.Resolve(config, new TestEnvironment(environment)));
        Assert.Contains("Jwt", error.Message);
        if (!string.IsNullOrWhiteSpace(key)) Assert.DoesNotContain(key, error.Message);
    }

    [Theory]
    [InlineData("Development", DevelopmentKey)]
    [InlineData("Production", "PrivateTestKeyWhichIsAtLeastThirtyTwoBytesLong")]
    [InlineData("Staging", "PrivateTestKeyWhichIsAtLeastThirtyTwoBytesLong")]
    public void ExplicitValidKey_IsAccepted(string environment, string key)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = key }).Build();
        Assert.Equal(key, JwtSigningKey.Resolve(config, new TestEnvironment(environment)));
    }
}
