using System.Text;

namespace IdentityService.Api.Security;

public static class JwtSigningKey
{
    public static string Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        var key = configuration["Jwt:Key"] ?? configuration["Jwt__Key"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException("Configure Jwt:Key (Jwt__Key) with a signing secret of at least 32 UTF-8 bytes before starting the service.");

        if (!environment.IsDevelopment() &&
            key == "FleetFlowSuperSecretSecurityKey2026!#ForJWTTokenGeneration")
            throw new InvalidOperationException("The repository development JWT signing key is forbidden outside Development. Configure a private Jwt__Key.");

        return key;
    }
}
