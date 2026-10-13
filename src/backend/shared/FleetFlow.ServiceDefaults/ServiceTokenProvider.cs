using System.Net.Http.Json;
using FleetFlow.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace FleetFlow.ServiceDefaults;

public interface IServiceTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken);
}

public sealed class ServiceTokenProvider(HttpClient client, ServiceAuthenticationOptions options,
    string clientId, TimeProvider clock) : IServiceTokenProvider, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private string? token;
    private DateTimeOffset refreshAt;
    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled || !options.ClientSecrets.TryGetValue(clientId, out var secret))
            throw new InvalidOperationException("Service credentials are not configured for this service.");
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (token is not null && clock.GetUtcNow() < refreshAt) return token;
            var requestedAt = clock.GetUtcNow();
            using var response = await client.PostAsJsonAsync("internal/service-token", new ServiceTokenRequest(clientId, secret), cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException("IdentityService could not authenticate this service.", null, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ServiceTokenResponse>(cancellationToken);
            if (result is null || string.IsNullOrWhiteSpace(result.AccessToken) || result.TokenType != "Bearer" ||
                result.ExpiresIn is <= 30 or > ServiceAuthenticationOptions.LifetimeSeconds)
                throw new HttpRequestException("IdentityService returned an invalid service token response.");
            token = result.AccessToken;
            refreshAt = requestedAt.AddSeconds(result.ExpiresIn - 30);
            return token;
        }
        finally { gate.Release(); }
    }
    public void Dispose() => gate.Dispose();
}

public static class ServiceEndpoints
{
    public static Uri Identity(IConfiguration configuration, IHostEnvironment environment)
    {
        var raw = configuration["ServiceAuth:IdentityUrl"] ?? configuration["INTERNAL_IDENTITY_URL"] ?? "http://localhost:5001/";
        if (!Uri.TryCreate(raw.TrimEnd('/') + "/", UriKind.Absolute, out var url) ||
            !string.IsNullOrEmpty(url.UserInfo) || !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment) || url.AbsolutePath != "/")
            throw new InvalidOperationException("Configure a service Identity URL without credentials, query, fragment or path.");
        if (url.Scheme == Uri.UriSchemeHttps) return url;
        if (url.Scheme == Uri.UriSchemeHttp && environment.IsDevelopment() &&
            (url.IsLoopback || url.Host == "identity-service")) return url;
        throw new InvalidOperationException("Service credentials require HTTPS; HTTP is limited to local Development.");
    }
}
