using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using FleetFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
namespace FleetFlow.ServiceDefaults;
public sealed class AccountAccessUnavailableException : Exception;
public interface IAccountAccessReader
{
    Task<AccountAccessState?> ReadAsync(Guid userId, CancellationToken ct);
}
public sealed class RemoteAccountAccessReader(HttpClient http, IServiceTokenProvider tokens) : IAccountAccessReader
{
    public async Task<AccountAccessState?> ReadAsync(Guid userId, CancellationToken ct)
    {
        try {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/accounts/{userId}/access");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetTokenAsync(ct));
            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) {
                var missing = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(ct);
                if (missing.ValueKind == System.Text.Json.JsonValueKind.Object && missing.TryGetProperty("code", out var code) && code.GetString() == "account_not_found") return null;
                throw new AccountAccessUnavailableException();
            }
            if (!response.IsSuccessStatusCode) throw new AccountAccessUnavailableException();
            var state = await response.Content.ReadFromJsonAsync<AccountAccessState>(ct);
            if (state == null || state.UserId != userId || state.AccessVersion < 0) throw new AccountAccessUnavailableException();
            return state;
        } catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or InvalidOperationException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        { throw new AccountAccessUnavailableException(); }
    }
}
public sealed class AccountAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAccountAccessReader accounts)
    {
        var endpoint = context.GetEndpoint();
        var protectedUser = endpoint != null && !context.Request.Path.StartsWithSegments("/internal") && endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0 && endpoint.Metadata.GetMetadata<IAllowAnonymous>() == null &&
            context.User.Identity?.IsAuthenticated == true && !context.User.HasClaim("token_kind", "service");
        if (!protectedUser) { await next(context); return; }
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub"), out var id) || id == Guid.Empty ||
            !long.TryParse(context.User.FindFirstValue("access_version"), NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version < 0)
        { await Reject(context, 401, "session_revoked", "Your session is no longer valid. Sign in again."); return; }
        try {
            var state = await accounts.ReadAsync(id, context.RequestAborted);
            if (state == null || state.UserId != id || !state.IsActive || state.AccessVersion != version)
            { await Reject(context, 401, "session_revoked", "Your session is no longer valid. Sign in again."); return; }
            if (state.MustChangePassword && context.Request.Path.Value?.Equals("/api/auth/change-password", StringComparison.OrdinalIgnoreCase) != true)
            { context.Response.StatusCode = 403; await context.Response.WriteAsJsonAsync(new { message = "Password change required before accessing protected application resources.", mustChangePassword = true }, context.RequestAborted); return; }
            await next(context);
        } catch (AccountAccessUnavailableException)
        { await Reject(context, 503, "access_verification_unavailable", "Account access could not be verified. Try again when Identity is available."); }
    }
    private static Task Reject(HttpContext context, int status, string code, string message)
    {
        context.Response.StatusCode = status; context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsJsonAsync(new { code, message }, context.RequestAborted);
    }
}
public static class AccountAccessRegistration
{
    public static void AddRemoteAccountAccess(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment) =>
        services.AddHttpClient<IAccountAccessReader, RemoteAccountAccessReader>(http => {
            http.BaseAddress = ServiceEndpoints.Identity(configuration, environment); http.Timeout = TimeSpan.FromSeconds(5);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
    public static IApplicationBuilder UseAccountAccess(this IApplicationBuilder app) => app.UseMiddleware<AccountAccessMiddleware>();
}
