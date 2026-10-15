using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using FleetFlow.Contracts;
using IdentityService.Api.Data;
using IdentityService.Api.Dtos;
using IdentityService.Api.Entities;
using IdentityService.Api.Onboarding;
using IdentityService.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace FleetFlow.IdentityService.Tests;
public sealed class GoogleOnboardingFlowTests
{
    private sealed class KeyHandler(RSA rsa):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {var key=rsa.ExportParameters(false);return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{keys=new[]{new{kty="RSA",kid="flow",alg="RS256",use="sig",n=Base64UrlEncoder.Encode(key.Modulus),e=Base64UrlEncoder.Encode(key.Exponent)}}}))});}
    }
    private sealed class Fixture:IDisposable
    {
        public IdentityDbContext Db=new(new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public IdentityVault Vault;public OnboardingService Accounts;public GoogleAuthentication Google;public PasswordHasher<User> Passwords=new();
        private RSA rsa=RSA.Create(2048);private HttpClient client;
        public Fixture()
        {
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ONBOARDING_ENABLED"]="true",["ONBOARDING_DATA_KEY"]=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),["ONBOARDING_HASH_KEY"]=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),["GOOGLE_CLIENT_ID"]="flow-client",["Jwt:Key"]=new string('t',64),["Jwt:Issuer"]="Tests",["Jwt:Audience"]="Tests",["FRONTEND_PUBLIC_URL"]="http://localhost:5174"}).Build();
            Vault=new(config);var authentication=new AuthenticationService(Db,Passwords,config);Accounts=new(Db,Vault,Passwords,authentication,config);
            client=new(new KeyHandler(rsa));var factory=Mock.Of<IHttpClientFactory>(x=>x.CreateClient("GoogleKeys")==client);
            Google=new(Db,Vault,Accounts,new GoogleTokenReader(factory,config),authentication,config);Db.Roles.Add(new(){Name="CUSTOMER"});Db.SaveChanges();
        }
        public static JsonElement Json(object value)=>JsonSerializer.SerializeToElement(value);
        public async Task<GoogleInput> Input(Guid? owner=null,bool security=false,string subject="google-subject",string email="synthetic@gmail.com")
        {
            var start=Json(await Google.Begin(owner,security));var token=new JwtSecurityToken("https://accounts.google.com","flow-client",[new("sub",subject),new("email",email),new("email_verified","true"),new("name","Google Supplied Name"),new("nonce",start.GetProperty("nonce").GetString()!)],DateTime.UtcNow.AddMinutes(-1),DateTime.UtcNow.AddMinutes(5),new SigningCredentials(new RsaSecurityKey(rsa){KeyId="flow"},SecurityAlgorithms.RsaSha256));
            return new(start.GetProperty("challengeId").GetGuid(),new JwtSecurityTokenHandler().WriteToken(token),null,null);
        }
        public async Task<User> GoogleUser()
        {var user=new User{FullName="Synthetic Driver",Username="existing_driver",Email="synthetic@gmail.com",HasLocalPassword=false,PasswordHash="",EmailVerificationMethod="Google"};user.Roles.Add(Db.Roles.Single());Db.Users.Add(user);Db.ExternalAccounts.Add(new(){UserId=user.Id,Subject="google-subject"});await Db.SaveChangesAsync();return user;}
        public string Code()=>Regex.Match(Vault.Decrypt<TransactionalEmail>(Db.Operations.Where(x=>x.Kind=="Email").OrderByDescending(x=>x.CreatedAt).First().Ciphertext,"email").Message,@"\b[0-9]{6}\b").Value;
        public void Dispose(){Db.Dispose();client.Dispose();rsa.Dispose();}
    }
    [Fact]public async Task NewGoogleIdentityCompletesProfileAfterVerificationAndExistingIdentitySignsInWithoutIt()
    {
        using var f=new Fixture();var pending=Fixture.Json(await f.Google.Complete(await f.Input(),null,default));
        Assert.Equal("ProfileRequired",pending.GetProperty("authenticationStatus").GetString());Assert.False(pending.TryGetProperty("Token",out _));Assert.Empty(f.Db.Users);Assert.Empty(f.Db.Operations.Where(x=>x.Kind=="Email"));
        var finish=new GoogleSignupInput(pending.GetProperty("signupId").GetGuid(),"chosen_driver","Chosen Full Name");
        var session=Assert.IsType<LoginResponse>(await f.Google.FinishSignup(finish,default));Assert.NotEmpty(session.Token);
        var user=Assert.Single(f.Db.Users);Assert.False(user.HasLocalPassword);Assert.Equal("CUSTOMER",Assert.Single(user.Roles).Name);
        var welcome=f.Vault.Decrypt<TransactionalEmail>(Assert.Single(f.Db.Operations.Where(x=>x.Kind=="Email")).Ciphertext,"email");Assert.Contains("Hi Chosen Full Name",welcome.Message);
        await Assert.ThrowsAsync<OnboardingException>(()=>f.Google.FinishSignup(finish,default));
        var existing=Assert.IsType<LoginResponse>(await f.Google.Complete(await f.Input(),null,default));Assert.Equal(user.Id,existing.User.Id);Assert.Single(f.Db.Users);Assert.Single(f.Db.Operations.Where(x=>x.Kind=="Email"));
    }
    [Fact]public async Task ProfileCompletionNeedsLiveProofAndAllowsCorrectingAClaimedUsername()
    {
        using var f=new Fixture();var pending=Fixture.Json(await f.Google.Complete(await f.Input(),null,default));var id=pending.GetProperty("signupId").GetGuid();
        f.Db.Users.Add(new(){Username="taken_driver",Email="other@example.test"});await f.Db.SaveChangesAsync();
        Assert.Equal(409,(await Assert.ThrowsAsync<OnboardingException>(()=>f.Google.FinishSignup(new(id,"taken_driver","New Driver"),default))).Status);
        Assert.Equal("Pending",f.Db.Operations.Single(x=>x.Id==id).State);
        f.Db.Operations.Single(x=>x.Id==id).ExpiresAt=DateTime.UtcNow.AddSeconds(-1);await f.Db.SaveChangesAsync();
        Assert.Equal(400,(await Assert.ThrowsAsync<OnboardingException>(()=>f.Google.FinishSignup(new(id,"new_driver","New Driver"),default))).Status);
    }
    [Fact]public async Task MatchingLocalEmailNeverAutomaticallyLinksGoogle()
    {
        using var f=new Fixture();f.Db.Users.Add(new(){Username="local_driver",Email="synthetic@gmail.com"});await f.Db.SaveChangesAsync();
        Assert.Equal(409,(await Assert.ThrowsAsync<OnboardingException>(async()=>await f.Google.Complete(await f.Input(),null,default))).Status);Assert.Empty(f.Db.ExternalAccounts);
    }
    [Fact]public async Task GoogleOnlyEmailChangeNeedsTheLinkedSubjectAndConsumesItsGrantOnce()
    {
        using var f=new Fixture();var user=await f.GoogleUser();
        Assert.Equal(403,(await Assert.ThrowsAsync<OnboardingException>(async()=>await f.Google.ConfirmSecurity(await f.Input(user.Id,true,"unrelated-subject"),user.Id,default))).Status);
        var proof=Fixture.Json(await f.Google.ConfirmSecurity(await f.Input(user.Id,true),user.Id,default)).GetProperty("reauthenticationId").GetGuid();
        var request=Fixture.Json(await f.Accounts.RequestEmailChange(user.Id,new("",NewEmail:"replacement@example.test",ReauthenticationId:proof)));
        Assert.Equal("synthetic@gmail.com",user.Email);
        Assert.Equal(401,(await Assert.ThrowsAsync<OnboardingException>(()=>f.Accounts.RequestSetPassword(user.Id,new("",ReauthenticationId:proof)))).Status);
        await f.Accounts.Verify(new(request.GetProperty("challengeId").GetGuid(),f.Code()));Assert.Equal("replacement@example.test",user.Email);
    }
    [Fact]public async Task GoogleOnlyPasswordCreationRequiresItsOwnEmailChallengeAndRevokesPreviousSessions()
    {
        using var f=new Fixture();var user=await f.GoogleUser();var version=user.AccessVersion;
        var proof=Fixture.Json(await f.Google.ConfirmSecurity(await f.Input(user.Id,true),user.Id,default)).GetProperty("reauthenticationId").GetGuid();
        var request=Fixture.Json(await f.Accounts.RequestSetPassword(user.Id,new("",ReauthenticationId:proof)));var id=request.GetProperty("challengeId").GetGuid();var code=f.Code();
        await Assert.ThrowsAsync<OnboardingException>(()=>f.Accounts.Verify(new(id,code),"StrongPassword@123",user.Id,"Recovery"));Assert.False(user.HasLocalPassword);
        var result=Fixture.Json(await f.Accounts.Verify(new(id,code),"StrongPassword@123",user.Id,"SetPassword"));Assert.True(result.TryGetProperty("session",out _));Assert.True(user.HasLocalPassword);Assert.True(user.AccessVersion>version);
        Assert.NotEqual(PasswordVerificationResult.Failed,f.Passwords.VerifyHashedPassword(user,user.PasswordHash,"StrongPassword@123"));
    }
}
