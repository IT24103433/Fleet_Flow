using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using FleetFlow.Contracts;
using FleetFlow.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
namespace FleetFlow.ServiceFoundation.Tests;
public sealed class AccountAccessTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private sealed class Reader(AccountAccessState? state, bool down = false) : IAccountAccessReader
    {
        public int Calls;
        public Task<AccountAccessState?> ReadAsync(Guid id, CancellationToken ct) { Calls++; return down ? throw new AccountAccessUnavailableException() : Task.FromResult(state); }
    }
    private static DefaultHttpContext Context(string? version = "0", string path = "/api/protected", bool anonymous = false, bool service = false)
    {
        var context = new DefaultHttpContext(); context.Request.Path = path; context.Response.Body = new MemoryStream();
        List<Claim> claims = [new("sub", UserId.ToString())]; if (version != null) claims.Add(new("access_version", version)); if (service) claims.Add(new("token_kind", "service"));
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(anonymous ? (object)new AllowAnonymousAttribute() : new AuthorizeAttribute()), "test"));
        return context;
    }
    [Theory]
    [InlineData("0", 0, true, 200)] [InlineData("0", 1, true, 401)] [InlineData("0", 0, false, 401)]
    [InlineData(null, 0, true, 401)] [InlineData("-1", 0, true, 401)] [InlineData("invalid", 0, true, 401)]
    public async Task ProtectedRequestsRequireAnActiveMatchingVersion(string? claim, long current, bool active, int status)
    {
        var context = Context(claim); var reader = new Reader(new(UserId, current, active, false)); var entered = false;
        await new AccountAccessMiddleware(_ => { entered = true; return Task.CompletedTask; }).InvokeAsync(context, reader);
        Assert.Equal(status, context.Response.StatusCode); Assert.Equal(status == 200, entered);
    }
    [Fact] public async Task DeletedAccountsAndIdentityOutagesDoNotEnterTheProtectedOperation()
    {
        foreach (var (reader, status) in new[] { (new Reader(null), 401), (new Reader(null, true), 503) }) {
            var context = Context(); await new AccountAccessMiddleware(_ => throw new Exception("Protected operation must not run")).InvokeAsync(context, reader);
            Assert.Equal(1, reader.Calls);
            Assert.Equal(status, context.Response.StatusCode);
        }
    }
    [Fact] public async Task AnonymousAndInternalScopeRequestsDoNotUseUserAccountValidation()
    {
        foreach (var context in new[] { Context(null, anonymous: true), Context(null, path: "/internal/reservations"), Context(null, service: true) }) {
            var reader = new Reader(null, true); var entered = false;
            await new AccountAccessMiddleware(_ => { entered = true; return Task.CompletedTask; }).InvokeAsync(context, reader);
            Assert.True(entered); Assert.Equal(0, reader.Calls);
        }
    }
    [Theory] [InlineData("/api/auth/change-password", 200)] [InlineData("/api/protected", 403)]
    public async Task CurrentForcedPasswordStateAllowsOnlyThePasswordChange(string path, int status)
    {
        var context = Context(path: path); var entered = false;
        await new AccountAccessMiddleware(_ => { entered = true; return Task.CompletedTask; }).InvokeAsync(context, new Reader(new(UserId, 0, true, true)));
        Assert.Equal(status, context.Response.StatusCode); Assert.Equal(status == 200, entered);
    }
    private sealed class Token : IServiceTokenProvider { public Task<string> GetTokenAsync(CancellationToken ct) => Task.FromResult("internal-test-token"); }
    private sealed class Handler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            Assert.Equal(new AuthenticationHeaderValue("Bearer", "internal-test-token"), request.Headers.Authorization);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("{\"code\":\"account_not_found\"}", Encoding.UTF8, "application/json") });
        }
    }
    [Fact] public async Task InternalReaderRejectsAuthorizationFailuresAndTreatsMissingAccountsAsMissing()
    {
        var unavailable = new RemoteAccountAccessReader(new HttpClient(new Handler(HttpStatusCode.Forbidden)) { BaseAddress = new Uri("http://127.0.0.1/") }, new Token());
        await Assert.ThrowsAsync<AccountAccessUnavailableException>(() => unavailable.ReadAsync(UserId, default));
        var missing = new RemoteAccountAccessReader(new HttpClient(new Handler(HttpStatusCode.NotFound)) { BaseAddress = new Uri("http://127.0.0.1/") }, new Token());
        Assert.Null(await missing.ReadAsync(UserId, default));
    }
    [Fact] public async Task UnexpectedErrorsNeverReturnExceptionDetailsOrSecrets()
    {
        var context = Context();
        await new ApiErrorsMiddleware(_ => throw new Exception("private-password-and-database-details"), NullLogger<ApiErrorsMiddleware>.Instance).InvokeAsync(context);
        context.Response.Body.Position = 0; var body = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();
        Assert.Equal(500, context.Response.StatusCode); Assert.Equal("application/problem+json", context.Response.ContentType);
        Assert.DoesNotContain("private-password", body); Assert.DoesNotContain("Exception", body);
    }
}
