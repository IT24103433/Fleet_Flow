using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using IdentityService.Api.Data;
using IdentityService.Api.Entities;
using IdentityService.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Api.Onboarding;

public sealed class GoogleTokenReader(IHttpClientFactory clients,IConfiguration configuration)
{
    public async Task<GoogleIdentity> Verify(string credential,string nonce,CancellationToken ct)
    {
        var audience=configuration["GOOGLE_CLIENT_ID"];
        if(string.IsNullOrWhiteSpace(audience))throw new OnboardingException(503,"Google sign-in is not configured.");
        if(string.IsNullOrWhiteSpace(credential)||credential.Length>10000)throw new OnboardingException(400,"Invalid Google response.");
        try
        {
            var json=await clients.CreateClient("GoogleKeys").GetStringAsync("https://www.googleapis.com/oauth2/v3/certs",ct);
            var handler=new JwtSecurityTokenHandler{MapInboundClaims=false};
            var principal=handler.ValidateToken(credential,new TokenValidationParameters{
                ValidateIssuerSigningKey=true,IssuerSigningKeys=new JsonWebKeySet(json).GetSigningKeys(),RequireSignedTokens=true,
                ValidateIssuer=true,ValidIssuers=["accounts.google.com","https://accounts.google.com"],ValidateAudience=true,ValidAudience=audience,
                RequireExpirationTime=true,ValidateLifetime=true,ClockSkew=TimeSpan.FromSeconds(30),ValidAlgorithms=[SecurityAlgorithms.RsaSha256]},out _);
            string Claim(string name)=>principal.FindFirst(name)?.Value??"";
            if(!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Claim("nonce")),Encoding.UTF8.GetBytes(nonce))||Claim("email_verified")!="true"||Claim("sub").Length is <1 or >255)
                throw new OnboardingException(401,"Google verification failed. Start again.");
            var email=Claim("email");if(!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email)||email.Length>100)throw new OnboardingException(401,"Google did not provide a valid verified email.");
            var domain=email[(email.LastIndexOf('@')+1)..];var hosted=Claim("hd");
            var authoritative=domain.Equals("gmail.com",StringComparison.OrdinalIgnoreCase)||(hosted.Length>0&&domain.Equals(hosted,StringComparison.OrdinalIgnoreCase));
            return new(Claim("sub"),email,Claim("name"),authoritative);
        }
        catch(SecurityTokenException){throw new OnboardingException(401,"Google verification failed. Start again.");}
        catch(ArgumentException){throw new OnboardingException(401,"Google verification failed. Start again.");}
        catch(HttpRequestException){throw new OnboardingException(503,"Google verification is temporarily unavailable. Try email sign-in.");}
        catch(OperationCanceledException)when(!ct.IsCancellationRequested){throw new OnboardingException(503,"Google verification is temporarily unavailable. Try email sign-in.");}
    }
}
public sealed record GoogleIdentity(string Subject,string Email,string Name,bool Authoritative);
public sealed class GoogleAuthentication(IdentityDbContext db,IdentityVault vault,OnboardingService accounts,
    GoogleTokenReader reader,AuthenticationService authentication,IConfiguration configuration)
{
    public async Task<object> Begin(Guid? userId,bool security=false)
    {
        accounts.RequireEnabled();if(string.IsNullOrWhiteSpace(configuration["GOOGLE_CLIENT_ID"]))throw new OnboardingException(503,"Google sign-in is not configured.");
        if(security&&userId==null)throw new OnboardingException(401,"Sign in before confirming your Google account.");
        var nonce=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));var row=new IdentityOperation{UserId=userId,Kind=security?"GoogleSecurityNonce":"GoogleNonce",State="Pending",ExpiresAt=DateTime.UtcNow.AddMinutes(5),Ciphertext=vault.Encrypt(nonce,"GoogleNonce")};
        db.Operations.Add(row);await db.SaveChangesAsync();return new{challengeId=row.Id,nonce,clientId=configuration["GOOGLE_CLIENT_ID"]};
    }
    public async Task<object> Complete(GoogleInput input,Guid? linkUser,CancellationToken ct)
    {
        accounts.RequireEnabled();await using var tx=db.Database.IsNpgsql()?await db.Database.BeginTransactionAsync(ct):null;
        if(db.Database.IsNpgsql())await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({input.ChallengeId.ToString()},0))",ct);
        var row=await db.Operations.SingleOrDefaultAsync(x=>x.Id==input.ChallengeId&&x.Kind=="GoogleNonce",ct);
        if(row==null||row.State!="Pending"||row.ExpiresAt<=DateTime.UtcNow||row.UserId!=linkUser)throw new OnboardingException(401,"This Google request expired or belongs to another session.");
        var google=await reader.Verify(input.Credential,vault.Decrypt<string>(row.Ciphertext,"GoogleNonce"),ct);
        var external=await db.ExternalAccounts.SingleOrDefaultAsync(x=>x.Provider=="Google"&&x.Subject==google.Subject,ct);
        User user;
        if(linkUser is {} owner)
        {
            user=await accounts.Account(owner);Customer(user);
            await accounts.Reauthenticate(owner,input.Password??"",input.RecoveryCode);
            if(external!=null&&external.UserId!=owner)throw new OnboardingException(409,"This Google account is linked to another FleetFlow account.");
            if(external==null)db.ExternalAccounts.Add(new(){UserId=owner,Subject=google.Subject});
            accounts.Audit(owner,owner,"GoogleLinked","Google sign-in explicitly linked after reauthentication.");
        }
        else if(external!=null){user=await accounts.Account(external.UserId);Customer(user);}
        else
        {
            if(!await accounts.Available("email",google.Email))throw new OnboardingException(409,"This email already has a FleetFlow account. Sign in normally and link Google from your account settings.");
            var proof=new IdentityOperation{Kind="GoogleSignupProof",State="Pending",ExpiresAt=DateTime.UtcNow.AddMinutes(10),Ciphertext=vault.Encrypt(google,"GoogleSignupProof")};
            db.Operations.Add(proof);row.State="Consumed";row.Ciphertext="";row.Version=Guid.NewGuid();
            await db.SaveChangesAsync(ct);if(tx!=null)await tx.CommitAsync(ct);
            return new{authenticationStatus="ProfileRequired",signupId=proof.Id,fullName=google.Name,email=google.Email,expiresAt=proof.ExpiresAt};
        }
        row.State="Consumed";row.Ciphertext="";row.Version=Guid.NewGuid();await db.SaveChangesAsync(ct);if(tx!=null)await tx.CommitAsync(ct);
        if(linkUser!=null)return new{success=true};
        if(user.EmailSecondStepEnabled)return await accounts.Challenge("Login",new(user.Email,user.Id,user.AccessVersion));
        return authentication.CreateSession(user);
    }
    public async Task<object> FinishSignup(GoogleSignupInput input,CancellationToken ct)
    {
        accounts.RequireEnabled();await using var tx=db.Database.IsNpgsql()?await db.Database.BeginTransactionAsync(ct):null;
        if(db.Database.IsNpgsql())await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({input.SignupId.ToString()},0))",ct);
        var row=await db.Operations.SingleOrDefaultAsync(x=>x.Id==input.SignupId&&x.Kind=="GoogleSignupProof",ct);
        if(row==null||row.State!="Pending"||row.ExpiresAt<=DateTime.UtcNow)throw new OnboardingException(400,"Your Google signup expired. Select your Google account again.");
        var google=vault.Decrypt<GoogleIdentity>(row.Ciphertext,"GoogleSignupProof");
        var username=input.Username?.Trim()??"";var name=input.FullName?.Trim()??"";
        if(!Regex.IsMatch(username,@"^[a-zA-Z0-9_-]{3,50}$")||name.Length is <2 or >100)throw new OnboardingException(400,"Choose an available username and enter your full name.");
        if(!await accounts.Available("username",username))throw new OnboardingException(409,"That username is already in use. Choose another one.");
        if(!await accounts.Available("email",google.Email)||await db.ExternalAccounts.AnyAsync(x=>x.Provider=="Google"&&x.Subject==google.Subject,ct))throw new OnboardingException(409,"This account is now registered. Return to sign-in.");
        row.State="Consumed";row.Ciphertext="";row.Version=Guid.NewGuid();
        if(!google.Authoritative)
        {
            var challenge=await accounts.Challenge("GoogleSignup",new(google.Email,null,0,username,name,"",GoogleSubject:google.Subject));
            if(tx!=null)await tx.CommitAsync(ct);return challenge;
        }
        var user=new User{FullName=name,Username=username,Email=google.Email,PasswordHash="",HasLocalPassword=false,EmailVerificationMethod="Google"};
        user.Roles.Add(await db.Roles.SingleAsync(x=>x.Name=="CUSTOMER",ct));db.Users.Add(user);db.ExternalAccounts.Add(new(){UserId=user.Id,Subject=google.Subject});
        accounts.QueueEmail(new(Guid.NewGuid(),user.Id,user.Email,"Welcome","Welcome to FleetFlow",$"Hi {name},\n\nYour FleetFlow account is ready, and Google sign-in is connected. Browse the fleet and complete driving verification when you’re ready to book.",ActionUrl:accounts.Frontend("#/customer-profile?section=driving")));
        accounts.Audit(user.Id,user.Id,"GoogleSignup","Google identity verified and customer profile completed.");
        await db.SaveChangesAsync(ct);if(tx!=null)await tx.CommitAsync(ct);return authentication.CreateSession(user);
    }
    public async Task<object> ConfirmSecurity(GoogleInput input,Guid owner,CancellationToken ct)
    {
        accounts.RequireEnabled();await using var tx=db.Database.IsNpgsql()?await db.Database.BeginTransactionAsync(ct):null;
        if(db.Database.IsNpgsql())await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({input.ChallengeId.ToString()},0))",ct);
        var row=await db.Operations.SingleOrDefaultAsync(x=>x.Id==input.ChallengeId&&x.Kind=="GoogleSecurityNonce"&&x.UserId==owner,ct);
        if(row==null||row.State!="Pending"||row.ExpiresAt<=DateTime.UtcNow)throw new OnboardingException(401,"Start a fresh Google confirmation.");
        var google=await reader.Verify(input.Credential,vault.Decrypt<string>(row.Ciphertext,"GoogleNonce"),ct);
        var user=await accounts.Account(owner);Customer(user);
        if(!await db.ExternalAccounts.AnyAsync(x=>x.UserId==owner&&x.Provider=="Google"&&x.Subject==google.Subject,ct))throw new OnboardingException(403,"Choose the Google account linked to this FleetFlow account.");
        row.State="Consumed";row.Ciphertext="";row.Version=Guid.NewGuid();
        var grant=new IdentityOperation{UserId=owner,Kind="GoogleSecurityGrant",State="Available",ExpiresAt=DateTime.UtcNow.AddMinutes(5),Ciphertext=vault.Encrypt(user.AccessVersion,"GoogleSecurityGrant")};
        db.Operations.Add(grant);await db.SaveChangesAsync(ct);if(tx!=null)await tx.CommitAsync(ct);
        return new{reauthenticationId=grant.Id,expiresAt=grant.ExpiresAt};
    }
    private static void Customer(User user)
    {if(!user.Roles.Any(x=>x.Name=="CUSTOMER")||user.Roles.Any(x=>x.Name is "ADMIN" or "FLEET_MANAGER" or "MAINTENANCE_STAFF"))throw new OnboardingException(403,"Google sign-in is available only to customer accounts.");}
}
