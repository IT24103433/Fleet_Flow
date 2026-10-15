using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using IdentityService.Api.Data;
using IdentityService.Api.Dtos;
using IdentityService.Api.Entities;
using IdentityService.Api.Onboarding;
using IdentityService.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;

namespace FleetFlow.IdentityService.Tests;

public sealed class VerifiedOnboardingTests
{
    private sealed class Fixture : IDisposable
    {
        public IdentityDbContext Db {get;}=new(new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public IdentityVault Vault {get;}
        public OnboardingService Service {get;}
        public IConfiguration Configuration {get;}
        public PasswordHasher<User> Passwords {get;}=new();
        public Fixture()
        {
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
                ["ONBOARDING_ENABLED"]="true",["ONBOARDING_DATA_KEY"]=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                ["ONBOARDING_HASH_KEY"]=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),["Jwt:Key"]=new string('t',64),
                ["Jwt:Issuer"]="Tests",["Jwt:Audience"]="Tests",["FRONTEND_PUBLIC_URL"]="http://localhost:5174"}).Build();
            Vault=new(config);Service=new(Db,Vault,Passwords,new AuthenticationService(Db,Passwords,config),config);
            Configuration=config;
            Db.Roles.Add(new(){Name="CUSTOMER"});Db.SaveChanges();
        }
        public async Task<User> User(bool initial=false,bool secondStep=false)
        {
            var user=new User{Username="test_driver",FullName="Synthetic Test Driver",Email="test@example.test",NeedsInitialEmailOtp=initial,EmailSecondStepEnabled=secondStep};
            user.PasswordHash=Passwords.HashPassword(user,"Example@123");user.Roles.Add(Db.Roles.Single());Db.Users.Add(user);await Db.SaveChangesAsync();return user;
        }
        public static Guid ChallengeId(object response)=>JsonSerializer.SerializeToElement(response).GetProperty("challengeId").GetGuid();
        public string Code()=>Regex.Match(Vault.Decrypt<FleetFlow.Contracts.TransactionalEmail>(Db.Operations.Where(x=>x.Kind=="Email").OrderByDescending(x=>x.CreatedAt).First().Ciphertext,"email").Message,@"\b\d{6}\b").Value;
        public void Dispose()=>Db.Dispose();
    }
    [Fact] public async Task SignupCreatesNoAccountOrTokenUntilOtpAndRejectsReplay()
    {
        using var f=new Fixture();var challenge=await f.Service.Signup(new(){FullName="Synthetic Driver",Username="new_driver",Email="new@example.test",Password="Example@123",ConfirmPassword="Example@123"});
        Assert.Empty(f.Db.Users);Assert.False(JsonSerializer.SerializeToElement(challenge).TryGetProperty("Token",out _));
        var id=Fixture.ChallengeId(challenge);var code=f.Code();var session=Assert.IsType<LoginResponse>(await f.Service.Verify(new(id,code)));
        Assert.NotEmpty(session.Token);Assert.Single(f.Db.Users);Assert.Equal("Otp",f.Db.Users.Single().EmailVerificationMethod);
        await Assert.ThrowsAsync<OnboardingException>(()=>f.Service.Verify(new(id,code)));
    }
    [Fact] public async Task WrongOtpLocksAtFiveAttemptsAndExpiredCodesCannotCreateUsers()
    {
        using var f=new Fixture();var response=await f.Service.Signup(new(){FullName="Synthetic Driver",Username="new_driver",Email="new@example.test",Password="Example@123",ConfirmPassword="Example@123"});
        var id=Fixture.ChallengeId(response);var real=f.Code();var wrong=real=="000000"?"111111":"000000";
        for(var i=0;i<5;i++)await Assert.ThrowsAsync<OnboardingException>(()=>f.Service.Verify(new(id,wrong)));
        Assert.Equal("Locked",f.Db.Operations.Single(x=>x.Id==id).State);Assert.Empty(f.Db.Users);
        await Assert.ThrowsAsync<OnboardingException>(()=>f.Service.Verify(new(id,real)));
    }
    [Fact] public async Task ResendSupersedesOldChallengeAndCooldownDoesNotConsumeIt()
    {
        using var f=new Fixture();var u=await f.User(true);var response=await f.Service.Challenge("Login",new(u.Email,u.Id,u.AccessVersion));var id=Fixture.ChallengeId(response);var old=f.Code();
        var error=await Assert.ThrowsAsync<OnboardingException>(()=>f.Service.Resend(id));Assert.Equal(429,error.Status);Assert.Equal("Pending",f.Db.Operations.Single(x=>x.Id==id).State);
        f.Db.Operations.Single(x=>x.Id==id).CreatedAt=DateTime.UtcNow.AddSeconds(-61);await f.Db.SaveChangesAsync();
        var replacement=await f.Service.Resend(id);Assert.NotEqual(id,Fixture.ChallengeId(replacement));
        await Assert.ThrowsAsync<OnboardingException>(()=>f.Service.Verify(new(id,old)));
    }
    [Fact] public async Task InitialStaffVerificationAndOptionalSecondStepNeverReturnAnEarlyToken()
    {
        using var f=new Fixture();var u=await f.User(true);var result=await f.Service.Login(new(){UsernameOrEmail=u.Username,Password="Example@123",LoginChannel="customer"});
        Assert.False(JsonSerializer.SerializeToElement(result).TryGetProperty("Token",out _));
        var session=Assert.IsType<LoginResponse>(await f.Service.Verify(new(Fixture.ChallengeId(result),f.Code())));Assert.NotEmpty(session.Token);Assert.False(u.NeedsInitialEmailOtp);
    }
    [Fact] public async Task RecoveryIsPurposeBoundAndRevokesOldAccessVersion()
    {
        using var f=new Fixture();var u=await f.User();var result=await f.Service.RequestRecovery(new(u.Email));var id=Fixture.ChallengeId(result);var code=f.Code();var old=u.AccessVersion;
        await Assert.ThrowsAsync<OnboardingException>(()=>f.Service.Verify(new(id,code),expectedPurpose:"Signup"));
        await f.Service.Verify(new(id,code),"Replacement@123",expectedPurpose:"Recovery");Assert.True(u.AccessVersion>old);
        Assert.NotEqual(PasswordVerificationResult.Failed,f.Passwords.VerifyHashedPassword(u,u.PasswordHash,"Replacement@123"));
    }
    [Fact] public async Task UnknownRecoveryHasSameShapeAndPersistsNoEmail()
    {
        using var f=new Fixture();var response=await f.Service.RequestRecovery(new("unknown@example.test"));
        Assert.NotEqual(Guid.Empty,Fixture.ChallengeId(response));Assert.Empty(f.Db.Operations.Where(x=>x.Kind=="Email"));Assert.Single(f.Db.Operations.Where(x=>x.Kind=="Otp:Recovery"));
    }
    [Fact] public async Task EmailChangeKeepsOldAddressUntilOtpAndCollisionCannotBeOverridden()
    {
        using var f=new Fixture();var u=await f.User();var result=await f.Service.RequestEmailChange(u.Id,new("Example@123",NewEmail:"next@example.test"));
        Assert.Equal("test@example.test",u.Email);await f.Service.Verify(new(Fixture.ChallengeId(result),f.Code()));Assert.Equal("next@example.test",u.Email);
        Assert.False(await f.Service.Available("email"," NEXT@EXAMPLE.TEST "));
    }
    [Fact] public async Task BackupCodesAreSingleUseAndNeverReplaceInitialVerification()
    {
        using var f=new Fixture();var u=await f.User();var request=await f.Service.RequestSecondStep(u.Id,true,new("Example@123"));
        var completed=JsonSerializer.SerializeToElement(await f.Service.Verify(new(Fixture.ChallengeId(request),f.Code())));
        var codes=completed.GetProperty("recoveryCodes").EnumerateArray().Select(x=>x.GetString()!).ToArray();Assert.Equal(8,codes.Length);
        foreach(var op in f.Db.Operations.Where(x=>x.Kind.StartsWith("Otp:")))op.CreatedAt=DateTime.UtcNow.AddMinutes(-2);await f.Db.SaveChangesAsync();
        var login=await f.Service.Login(new(){UsernameOrEmail=u.Username,Password="Example@123",LoginChannel="customer"});
        Assert.IsType<LoginResponse>(await f.Service.Verify(new(Fixture.ChallengeId(login),codes[0],true)));
        foreach(var op in f.Db.Operations.Where(x=>x.Kind.StartsWith("Otp:")))op.CreatedAt=DateTime.UtcNow.AddMinutes(-2);await f.Db.SaveChangesAsync();
        u.NeedsInitialEmailOtp=true;var next=await f.Service.Login(new(){UsernameOrEmail=u.Username,Password="Example@123",LoginChannel="customer"});
        await Assert.ThrowsAsync<OnboardingException>(()=>f.Service.Verify(new(Fixture.ChallengeId(next),codes[1],true)));
    }
    [Theory]
    [InlineData("012345678v",true)][InlineData("001234567890",true)][InlineData("12345678V",false)][InlineData("123456789A",false)]
    public void NicFormatsPreserveLeadingZerosAndDoNotClaimAuthenticity(string number,bool accepted)=>Assert.Equal(accepted,DriverRules.ValidNic(number));
    [Fact] public void ForeignDocumentsAndSriLankanExpiryBoundaryUseTheirOwnRules()
    {
        var details=new DriverDetails{LegalName="Synthetic Driver",Address="Synthetic address",PhoneNumber="+94771234567",IdentityNumber="001234567890",LicenceNumber="B0000001",LicenceExpiry=new DateOnly(2099,1,2)};
        Assert.Null(DriverRules.Error(details,false));Assert.True(DriverRules.CommonSriLicence(details.LicenceNumber));
        Assert.Equal(new DateTime(2099,1,2,18,30,0,DateTimeKind.Utc),DriverRules.ValidUntil(details));
        details.IssuingCountry="GB";Assert.NotNull(DriverRules.Error(details,false));details.IdentityType="Passport";details.IdentityNumber="FOREIGN123";details.PassportExpiry=new(2099,1,3);details.PermitNumber="IDP123";details.PermitExpiry=new(2099,1,3);details.LicenceNumber="UK-TEST/123";Assert.Null(DriverRules.Error(details,false));
    }
    [Fact] public void VaultRejectsCrossPurposeDecryptionAndDoesNotExposePlainIdentityNumbers()
    {
        using var f=new Fixture();var encrypted=f.Vault.Encrypt("001234567890","driver");Assert.DoesNotContain("001234567890",encrypted);
        Assert.ThrowsAny<CryptographicException>(()=>f.Vault.Decrypt<string>(encrypted,"email"));
    }
    private static DriverDetails Evidence()=>new(){LegalName="Synthetic Driver",PhoneNumber="+94771234567",Address="Synthetic review address",IdentityNumber="001234567890",LicenceNumber="B0000001",LicenceExpiry=new(2099,1,2),Photos=[new(Guid.NewGuid(),"DL","image/jpeg",100),new(Guid.NewGuid(),"NIC","image/jpeg",100)]};
    [Fact] public async Task DriverApprovalClaimsIdentifiersAndRejectsAnotherOwnersDuplicate()
    {
        using var f=new Fixture();var owner=await f.User();owner.EmailVerificationMethod="Otp";
        var revision=new IdentityOperation{UserId=owner.Id,Kind="Driver",State="Pending review",Ciphertext=f.Vault.Encrypt(Evidence(),"Driver")};f.Db.Operations.Add(revision);await f.Db.SaveChangesAsync();
        var drivers=new DriverVerificationService(f.Db,f.Vault,f.Service,new ConfigurationBuilder().Build());
        await drivers.Review(Guid.NewGuid(),revision.Id,new(true,"Synthetic evidence reviewed",["012345678V"]));Assert.Equal(3,f.Db.DocumentClaims.Count());
        Assert.True((await drivers.Eligibility(owner.Id,new DateTime(2099,1,2,18,29,59,DateTimeKind.Utc))).Eligible);
        Assert.False((await drivers.Eligibility(owner.Id,new DateTime(2099,1,2,18,30,0,DateTimeKind.Utc))).Eligible);
        var second=new User{Username="other_driver",Email="other@example.test"};second.Roles.Add(f.Db.Roles.Single());f.Db.Users.Add(second);
        var duplicate=new IdentityOperation{UserId=second.Id,Kind="Driver",State="Pending review",Ciphertext=f.Vault.Encrypt(Evidence(),"Driver")};f.Db.Operations.Add(duplicate);await f.Db.SaveChangesAsync();
        var rejected=await Assert.ThrowsAsync<OnboardingException>(()=>drivers.Review(Guid.NewGuid(),duplicate.Id,new(true,"Cannot override")));Assert.Equal(409,rejected.Status);
        Assert.All(f.Db.DocumentClaims,x=>Assert.Equal(owner.Id,x.UserId));
    }
    [Fact] public async Task LegalRevisionInvalidatesApprovalButContactEditingDoesNotReleaseClaims()
    {
        using var f=new Fixture();var owner=await f.User();owner.EmailVerificationMethod="Otp";var details=Evidence();
        var revision=new IdentityOperation{UserId=owner.Id,Kind="Driver",State="Pending review",Ciphertext=f.Vault.Encrypt(details,"Driver")};f.Db.Operations.Add(revision);await f.Db.SaveChangesAsync();
        var drivers=new DriverVerificationService(f.Db,f.Vault,f.Service,new ConfigurationBuilder().Build());await drivers.Review(Guid.NewGuid(),revision.Id,new(true,"Reviewed"));
        await f.Service.UpdateProfile(owner.Id,new(){FullName="Updated display name",Username=owner.Username,PhoneNumber="+94771234567",Address="Updated contact address"});
        Assert.True((await drivers.Eligibility(owner.Id,DateTime.UtcNow.AddDays(1))).Eligible);
        details.LegalName="Updated legal identity";await drivers.Save(owner.Id,details,false);
        Assert.False((await drivers.Eligibility(owner.Id,DateTime.UtcNow.AddDays(1))).Eligible);Assert.Equal(2,f.Db.DocumentClaims.Count());
    }
    [Fact] public async Task PrivateDocumentLookupRejectsUnrelatedCustomersBeforeReadingStorage()
    {
        using var f=new Fixture();var owner=await f.User();var unrelated=new User{Username="unrelated",Email="unrelated@example.test"};f.Db.Users.Add(unrelated);await f.Db.SaveChangesAsync();
        var photo=new DriverPhoto(Guid.NewGuid(),"NIC","image/jpeg",100);f.Db.Operations.Add(new(){Id=photo.Id,UserId=owner.Id,Kind="Photo",State="Stored",Ciphertext=f.Vault.Encrypt(photo,"photo")});await f.Db.SaveChangesAsync();
        var drivers=new DriverVerificationService(f.Db,f.Vault,f.Service,new ConfigurationBuilder().Build());
        var error=await Assert.ThrowsAsync<OnboardingException>(()=>drivers.Photo(unrelated.Id,photo.Id,false));Assert.Equal(404,error.Status);
    }
    [Fact] public void AHeaderDisguisedAsAnImageCannotPassDocumentDecoding()
    {
        var fake=new byte[64];new byte[]{137,80,78,71,13,10,26,10}.CopyTo(fake,0);"IHDR"u8.CopyTo(fake.AsSpan(12));
        Assert.Equal("image/png",DriverVerificationService.ImageType(fake));Assert.Throws<OnboardingException>(()=>DocumentImage.Sanitize(fake));
    }
    private sealed class LifecycleHandler:HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new StringContent(request.Method==HttpMethod.Get?"{\"hasOutstandingRentals\":false}":"{\"success\":true}",System.Text.Encoding.UTF8,"application/json")});
    }
    private static OnboardingService WithLifecycle(Fixture f)
    {
        var factory=new Moq.Mock<IHttpClientFactory>();factory.Setup(x=>x.CreateClient(Moq.It.IsAny<string>())).Returns(()=>new HttpClient(new LifecycleHandler()){BaseAddress=new Uri("http://loopback.test/")});
        var tokens=new Moq.Mock<FleetFlow.ServiceDefaults.IServiceTokenProvider>();tokens.Setup(x=>x.GetTokenAsync(Moq.It.IsAny<CancellationToken>())).ReturnsAsync("synthetic-internal-token");
        return new(f.Db,f.Vault,f.Passwords,new AuthenticationService(f.Db,f.Passwords,f.Configuration),f.Configuration,new AccountLifecycleGateway(factory.Object,tokens.Object));
    }
    [Fact]public async Task VerifiedDeletionRevokesAccessAndAuditedRestorationDoesNotReviveOldSessions()
    {
        using var f=new Fixture();var owner=await f.User();var service=WithLifecycle(f);var version=owner.AccessVersion;
        var request=await service.RequestDeletion(owner.Id);var result=JsonSerializer.SerializeToElement(await service.Verify(new(Fixture.ChallengeId(request),f.Code()),expectedUser:owner.Id,expectedPurpose:"DeleteAccount"));
        Assert.True(result.GetProperty("deleted").GetBoolean());Assert.False(owner.IsActive);Assert.NotNull(owner.DeletedAt);Assert.True(owner.AccessVersion>version);
        var deletion=Assert.Single(f.Db.Operations.Where(x=>x.Kind=="AccountDeletion"));Assert.NotEqual(Fixture.ChallengeId(request),deletion.Id);
        await service.RestoreAccount(Guid.NewGuid(),owner.Id,"Synthetic restoration reviewed");Assert.True(owner.IsActive);Assert.Null(owner.DeletedAt);Assert.True(owner.AccessVersion>version+1);
        Assert.Equal("Restored",deletion.State);
    }
    [Fact]public async Task AfterThirtyDaysCleanupReleasesClaimsAndAnonymisesWithoutDeletingAccountHistoryId()
    {
        using var f=new Fixture();var owner=await f.User();var id=owner.Id;var service=WithLifecycle(f);
        f.Db.DocumentClaims.Add(new(){UserId=id,Kind="LK:NIC",Fingerprint=new string('A',64),RevisionId=Guid.NewGuid()});await f.Db.SaveChangesAsync();
        var request=await service.RequestDeletion(id);await service.Verify(new(Fixture.ChallengeId(request),f.Code()),expectedUser:id,expectedPurpose:"DeleteAccount");
        owner.DeletedAt=DateTime.UtcNow.AddDays(-31);await f.Db.SaveChangesAsync();await service.SweepLifecycle(default);
        var anonymized=await f.Db.Users.SingleAsync(x=>x.Id==id);Assert.Equal("Anonymized",anonymized.EmailVerificationMethod);Assert.Empty(anonymized.PasswordHash);Assert.Empty(f.Db.DocumentClaims);
        Assert.DoesNotContain("test@example.test",anonymized.Email);Assert.Empty((object[])await service.RecycleBin());
        await Assert.ThrowsAsync<OnboardingException>(()=>service.RestoreAccount(Guid.NewGuid(),id,"Too late"));
    }
}
