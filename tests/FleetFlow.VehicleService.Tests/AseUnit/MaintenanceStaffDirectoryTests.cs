using System.Net;
using System.Net.Http.Json;
using FleetFlow.Contracts;
using FleetFlow.ServiceDefaults;
using FleetService.Api.Services;
using Moq;
using Moq.Protected;

namespace FleetFlow.VehicleService.Tests.AseUnit;

[Trait("Category", "ASE.Unit")]
[Trait("Owner", "Sanjeevsaran")]
public sealed class MaintenanceStaffDirectoryTests
{
    private static readonly Guid StaffId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly MaintenanceStaffAccount Staff = new(StaffId, "Synthetic Worker", "worker_unit");

    private sealed class Harness : IDisposable
    {
        public Mock<HttpMessageHandler> Handler { get; } = new(MockBehavior.Strict);
        public Mock<IServiceTokenProvider> Tokens { get; } = new(MockBehavior.Strict);
        public HttpClient Http { get; }
        public MaintenanceStaffDirectory Subject { get; }
        public List<(string Method, string Path, string? Scheme, string? Token)> Requests { get; } = [];

        public Harness(Func<CancellationToken, Task<HttpResponseMessage>> response)
        {
            Handler.Protected().Setup("Dispose", ItExpr.IsAny<bool>());
            Tokens.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("opaque-unit-service-token");
            Handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, _) => Requests.Add((request.Method.Method,
                    request.RequestUri!.AbsolutePath, request.Headers.Authorization?.Scheme, request.Headers.Authorization?.Parameter)))
                .Returns<HttpRequestMessage, CancellationToken>((_, ct) => response(ct));
            Http = new HttpClient(Handler.Object) { BaseAddress = new("https://identity.unit.invalid/") };
            Subject = new(Http, Tokens.Object);
        }

        public void VerifyOne(string path)
        {
            Assert.Equal(("GET", path, "Bearer", "opaque-unit-service-token"), Assert.Single(Requests));
            Tokens.Verify(t => t.GetTokenAsync(It.IsAny<CancellationToken>()), Times.Once);
            Tokens.VerifyNoOtherCalls();
            Handler.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
            Handler.VerifyNoOtherCalls();
        }

        public void Dispose() => Http.Dispose();
    }

    private static Task<HttpResponseMessage> Json(object value) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(value) });

    [Fact]
    public async Task Get_ValidAccount_UsesRestrictedServiceTokenAndPreservesIdentity()
    {
        using var harness = new Harness(_ => Json(Staff));
        Assert.Equal(Staff, await harness.Subject.GetAsync(StaffId, CancellationToken.None));
        harness.VerifyOne($"/internal/staff/maintenance/{StaffId}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1000)]
    public async Task List_ValidBoundaries_PreservesEveryAccount(int count)
    {
        var expected = Enumerable.Range(1, count).Select(i => new MaintenanceStaffAccount(
            Guid.Parse($"00000000-0000-4000-8000-{i:D12}"), $"Worker {i}", $"worker_{i}")).ToArray();
        using var harness = new Harness(_ => Json(expected));
        var result = await harness.Subject.ListAsync(CancellationToken.None);
        Assert.Equal(expected, result);
        harness.VerifyOne("/internal/staff/maintenance");
    }

    public static TheoryData<MaintenanceStaffAccount[]> InvalidLists => new()
    {
        new[] { Staff, Staff },
        new[] { Staff with { Id = Guid.Empty } },
        new[] { Staff with { Username = " " } },
        Enumerable.Range(1, 1001).Select(i => new MaintenanceStaffAccount(Guid.Parse($"00000000-0000-4000-8000-{i:D12}"), "Worker", "worker")).ToArray()
    };

    [Theory]
    [MemberData(nameof(InvalidLists))]
    public async Task List_InvalidIdentityDuplicateOrOverflow_FailsClosed(MaintenanceStaffAccount[] accounts)
    {
        using var harness = new Harness(_ => Json(accounts));
        await Assert.ThrowsAsync<MaintenanceStaffUnavailableException>(() => harness.Subject.ListAsync(CancellationToken.None));
        harness.VerifyOne("/internal/staff/maintenance");
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task DependencyHttpFailure_IsNotTreatedAsEligibleStaff(int status)
    {
        using var harness = new Harness(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)));
        await Assert.ThrowsAsync<MaintenanceStaffUnavailableException>(() => harness.Subject.GetAsync(StaffId, CancellationToken.None));
        harness.VerifyOne($"/internal/staff/maintenance/{StaffId}");
    }

    [Fact]
    public async Task NotFound_DiffersBetweenIndividualAndWholeDirectory()
    {
        using var single = new Harness(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        Assert.Null(await single.Subject.GetAsync(StaffId, CancellationToken.None));
        single.VerifyOne($"/internal/staff/maintenance/{StaffId}");
        using var list = new Harness(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        await Assert.ThrowsAsync<MaintenanceStaffUnavailableException>(() => list.Subject.ListAsync(CancellationToken.None));
        list.VerifyOne("/internal/staff/maintenance");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("[]")]
    public async Task InvalidJsonOrMissingBody_FailsClosed(string body)
    {
        using var harness = new Harness(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }));
        await Assert.ThrowsAsync<MaintenanceStaffUnavailableException>(() => harness.Subject.GetAsync(StaffId, CancellationToken.None));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WrongAccountOrBlankUsername_IsRejected(bool wrongId)
    {
        var account = wrongId ? Staff with { Id = Guid.Empty } : Staff with { Username = "" };
        using var harness = new Harness(_ => Json(account));
        await Assert.ThrowsAsync<MaintenanceStaffUnavailableException>(() => harness.Subject.GetAsync(StaffId, CancellationToken.None));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancellation_DistinguishesCallerCancellationFromDependencyTimeout(bool callerCancelled)
    {
        using var cancellation = new CancellationTokenSource();
        using var harness = new Harness(_ =>
        {
            if (callerCancelled) cancellation.Cancel();
            return Task.FromException<HttpResponseMessage>(new OperationCanceledException(cancellation.Token));
        });
        if (callerCancelled)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.Subject.GetAsync(StaffId, cancellation.Token));
        else
            await Assert.ThrowsAsync<MaintenanceStaffUnavailableException>(() => harness.Subject.GetAsync(StaffId, cancellation.Token));
    }

    [Fact]
    public async Task TokenFailure_NeverSendsHttpRequest()
    {
        using var harness = new Harness(_ => throw new InvalidOperationException("HTTP must not execute"));
        harness.Tokens.Setup(t => t.GetTokenAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Credentials unavailable"));
        await Assert.ThrowsAsync<MaintenanceStaffUnavailableException>(() => harness.Subject.GetAsync(StaffId, CancellationToken.None));
        Assert.Empty(harness.Requests);
        harness.Handler.Protected().Verify("SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }
}
