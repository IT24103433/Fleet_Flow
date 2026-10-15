using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using IdentityService.Api.Onboarding;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace FleetFlow.IdentityService.Tests;
public sealed class GoogleIdentityTests
{
    private sealed class KeysHandler(RSA rsa):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Assert.Equal("https://www.googleapis.com/oauth2/v3/certs",request.RequestUri!.AbsoluteUri);
            var key=rsa.ExportParameters(false);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{keys=new[]{new{kty="RSA",kid="test-key",alg="RS256",use="sig",n=Base64UrlEncoder.Encode(key.Modulus),e=Base64UrlEncoder.Encode(key.Exponent)}}}))});
        }
    }
    [Theory]
    [InlineData("valid",true)][InlineData("audience",false)][InlineData("issuer",false)][InlineData("nonce",false)][InlineData("expired",false)][InlineData("unverified",false)]
    public async Task SignatureAudienceIssuerNonceExpiryAndVerifiedEmailAreValidated(string scenario,bool accepted)
    {
        using var rsa=RSA.Create(2048);using var client=new HttpClient(new KeysHandler(rsa));
        var factory=Mock.Of<IHttpClientFactory>(x=>x.CreateClient("GoogleKeys")==client);
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["GOOGLE_CLIENT_ID"]="test-client"}).Build();
        var now=DateTime.UtcNow;var token=new JwtSecurityToken(scenario=="issuer"?"https://other.example":"https://accounts.google.com",scenario=="audience"?"other-client":"test-client",
            [new("sub","stable-google-subject"),new("nonce",scenario=="nonce"?"wrong-nonce":"expected-nonce"),new("email","synthetic@gmail.com"),new("email_verified",scenario=="unverified"?"false":"true"),new("name","Synthetic Driver")],
            now.AddHours(-1),scenario=="expired"?now.AddMinutes(-5):now.AddMinutes(5),new SigningCredentials(new RsaSecurityKey(rsa){KeyId="test-key"},SecurityAlgorithms.RsaSha256));
        var reader=new GoogleTokenReader(factory,configuration);var credential=new JwtSecurityTokenHandler().WriteToken(token);
        if(accepted){var identity=await reader.Verify(credential,"expected-nonce",default);Assert.True(identity.Authoritative);Assert.Equal("stable-google-subject",identity.Subject);}
        else{var error=await Assert.ThrowsAsync<OnboardingException>(()=>reader.Verify(credential,"expected-nonce",default));Assert.Equal(401,error.Status);}
    }
    [Fact]public async Task NonGmailWithoutValidatedWorkspaceStillNeedsFleetFlowEmailVerification()
    {
        using var rsa=RSA.Create(2048);using var client=new HttpClient(new KeysHandler(rsa));var factory=Mock.Of<IHttpClientFactory>(x=>x.CreateClient("GoogleKeys")==client);
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["GOOGLE_CLIENT_ID"]="test-client"}).Build();
        var token=new JwtSecurityToken("https://accounts.google.com","test-client",[new Claim("sub","foreign-email-subject"),new("nonce","nonce"),new("email","synthetic@example.test"),new("email_verified","true")],DateTime.UtcNow.AddMinutes(-1),DateTime.UtcNow.AddMinutes(5),new SigningCredentials(new RsaSecurityKey(rsa){KeyId="test-key"},SecurityAlgorithms.RsaSha256));
        Assert.False((await new GoogleTokenReader(factory,config).Verify(new JwtSecurityTokenHandler().WriteToken(token),"nonce",default)).Authoritative);
    }
}
