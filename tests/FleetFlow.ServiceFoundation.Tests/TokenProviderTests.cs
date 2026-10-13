using System.Net;
using System.Net.Http.Json;
using FleetFlow.Contracts;
using FleetFlow.ServiceDefaults;

namespace FleetFlow.ServiceFoundation.Tests;

public sealed class TokenProviderTests
{
    [Fact]
    public async Task ConcurrentCallsShareTokenAndRefreshBeforeExpiry()
    {
        var clock=new MutableClock(); var handler=new TokenHandler();
        using var client=new HttpClient(handler){BaseAddress=new Uri("http://localhost:5001/")};
        using var provider=new ServiceTokenProvider(client,SecurityTests.Options(),"rental-service",clock);
        var values=await Task.WhenAll(Enumerable.Range(0,20).Select(_=>provider.GetTokenAsync(default)));
        Assert.All(values,value=>Assert.Equal("token-1",value)); Assert.Equal(1,handler.Calls);
        clock.Advance(TimeSpan.FromSeconds(270));
        Assert.Equal("token-2",await provider.GetTokenAsync(default)); Assert.Equal(2,handler.Calls);
        Assert.Equal("http://localhost:5001/internal/service-token",handler.LastUrl);
        Assert.Equal("rental-service",handler.LastRequest?.ClientId);
        Assert.Null(handler.Authorization);
    }
    [Fact]
    public async Task FailedRefreshDoesNotReturnStaleTokenOrExposeBrokerBody()
    {
        var clock=new MutableClock();var handler=new TokenHandler();
        using var client=new HttpClient(handler){BaseAddress=new Uri("http://localhost:5001/")};
        using var provider=new ServiceTokenProvider(client,SecurityTests.Options(),"rental-service",clock);
        await provider.GetTokenAsync(default);clock.Advance(TimeSpan.FromMinutes(6));handler.Fail=true;
        var error=await Assert.ThrowsAsync<HttpRequestException>(()=>provider.GetTokenAsync(default));
        Assert.DoesNotContain(SecurityTests.Secret,error.Message);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,error.StatusCode);
    }
    [Fact]
    public async Task DisabledProviderDoesNotSendCredentials()
    {
        var handler=new TokenHandler();using var client=new HttpClient(handler){BaseAddress=new Uri("http://localhost:5001/")};
        using var provider=new ServiceTokenProvider(client,SecurityTests.Options(false),"rental-service",TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>provider.GetTokenAsync(default));Assert.Equal(0,handler.Calls);
    }
    private sealed class MutableClock:TimeProvider
    {
        private DateTimeOffset now=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>now;
        public void Advance(TimeSpan amount)=>now+=amount;
    }
    private sealed class TokenHandler:HttpMessageHandler
    {
        public int Calls;public bool Fail;public string? LastUrl;public string? Authorization;public ServiceTokenRequest? LastRequest;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            Calls++;LastUrl=request.RequestUri?.ToString();Authorization=request.Headers.Authorization?.ToString();
            LastRequest=await request.Content!.ReadFromJsonAsync<ServiceTokenRequest>(cancellationToken);
            return Fail?new(HttpStatusCode.ServiceUnavailable){Content=new StringContent(SecurityTests.Secret)}:
                new(HttpStatusCode.OK){Content=JsonContent.Create(new ServiceTokenResponse($"token-{Calls}","Bearer",300))};
        }
    }
}
