using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FleetFlow.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace FleetFlow.ServiceDefaults;

public sealed record ServiceAuthenticationOptions(bool Enabled, string SigningKey, string Issuer,
    IReadOnlyDictionary<string, string> ClientSecrets)
{
    public const string Scheme = "InternalService";
    public const string Audience = "FleetFlow.Internal";
    public const int LifetimeSeconds = 300;

    public static ServiceAuthenticationOptions Read(IConfiguration configuration, string userSigningKey)
    {
        var enabled = configuration.GetValue<bool?>("SERVICE_AUTH_ENABLED")
            ?? configuration.GetValue<bool>("ServiceAuth:Enabled");
        var key = configuration["ServiceAuth:SigningKey"] ?? configuration["SERVICE_AUTH_SIGNING_KEY"] ?? "";
        var issuer = configuration["Jwt:Issuer"] ?? "FleetFlow.IdentityService";
        var clients = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach (var id in ServiceScopes.ClientGrants.Keys)
        {
            var alias = "SERVICE_AUTH_" + id.Replace("-service", "").ToUpperInvariant() + "_SECRET";
            var secret = configuration[$"ServiceAuth:Clients:{id}"] ?? configuration[alias];
            if (secret is not null)
            {
                if (enabled && Encoding.UTF8.GetByteCount(secret) < 32)
                    throw new InvalidOperationException($"Configure a private service credential of at least 32 bytes for {id}.");
                clients[id] = secret;
            }
        }
        if (enabled && (Encoding.UTF8.GetByteCount(key) < 32 || key == userSigningKey))
            throw new InvalidOperationException("Internal service signing key must be private, at least 32 bytes, and different from the user JWT key.");
        return new(enabled, key, issuer, clients);
    }

    public TokenValidationParameters ValidationParameters() => new()
    {
        ValidateIssuer = true, ValidIssuer = Issuer,
        ValidateAudience = true, ValidAudience = Audience,
        ValidateLifetime = true, RequireExpirationTime = true,
        ValidateIssuerSigningKey = true, RequireSignedTokens = true,
        IssuerSigningKey = Enabled ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)) : null,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.Zero
    };
}

public sealed class ServiceTokenIssuer(ServiceAuthenticationOptions options, TimeProvider clock)
{
    public ServiceTokenResponse? Issue(ServiceTokenRequest request)
    {
        if (!options.Enabled || request.ClientId is null || request.ClientSecret is null ||
            !options.ClientSecrets.TryGetValue(request.ClientId, out var secret) ||
            !ServiceScopes.ClientGrants.TryGetValue(request.ClientId, out var grants)) return null;
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(secret)),
            SHA256.HashData(Encoding.UTF8.GetBytes(request.ClientSecret)))) return null;
        var now = clock.GetUtcNow().UtcDateTime;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.ClientId), new("token_kind", "service"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        claims.AddRange(grants.Select(scope => new Claim("scope", scope)));
        var token = new JwtSecurityToken(options.Issuer, ServiceAuthenticationOptions.Audience,
            claims, now, now.AddSeconds(ServiceAuthenticationOptions.LifetimeSeconds),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), "Bearer", ServiceAuthenticationOptions.LifetimeSeconds);
    }
}

public static class ServiceAuthentication
{
    public static string UserSigningKey(IConfiguration configuration, IHostEnvironment environment)
    {
        var key = configuration["Jwt:Key"] ?? configuration["JWT_SECRET"] ?? "";
        if (Encoding.UTF8.GetByteCount(key) < 32 || (!environment.IsDevelopment() &&
            key == "FleetFlowSuperSecretSecurityKey2026!#ForJWTTokenGeneration"))
            throw new InvalidOperationException("Configure a private Jwt__Key of at least 32 bytes.");
        return key;
    }

    public static void AddInternalServiceAuthentication(this IServiceCollection services,
        IConfiguration configuration, string userSigningKey)
    {
        var options = ServiceAuthenticationOptions.Read(configuration, userSigningKey);
        services.AddSingleton(options);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ServiceTokenIssuer>();
        services.AddAuthentication().AddJwtBearer(ServiceAuthenticationOptions.Scheme, bearer =>
        {
            bearer.MapInboundClaims = false;
            bearer.TokenValidationParameters = options.ValidationParameters();
            bearer.Events = new JwtBearerEvents
            {
                OnMessageReceived = context => { if (!options.Enabled) context.NoResult(); return Task.CompletedTask; }
            };
        });
        services.AddAuthorization(authorization =>
        {
            foreach (var scope in ServiceScopes.ClientGrants.Values.SelectMany(value => value).Distinct())
                authorization.AddPolicy(scope, policy => policy.AddAuthenticationSchemes(ServiceAuthenticationOptions.Scheme)
                    .RequireAuthenticatedUser().RequireClaim("token_kind", "service").RequireClaim("scope", scope));
        });
    }

    public static void AddServiceTokenClient(this IServiceCollection services, IConfiguration configuration,
        IHostEnvironment environment, string clientId)
    {
        if (!ServiceScopes.ClientGrants.ContainsKey(clientId)) throw new InvalidOperationException("Unknown service identity.");
        services.AddHttpClient("ServiceToken", client =>
        {
            client.BaseAddress = ServiceEndpoints.Identity(configuration, environment);
            client.Timeout = TimeSpan.FromSeconds(5);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddSingleton<IServiceTokenProvider>(provider => new ServiceTokenProvider(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("ServiceToken"),
            provider.GetRequiredService<ServiceAuthenticationOptions>(), clientId, provider.GetRequiredService<TimeProvider>()));
    }
}
