using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using FleetFlow.Contracts;
using IdentityService.Api.Data;
using IdentityService.Api.Dtos;
using IdentityService.Api.Entities;
using IdentityService.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Api.Onboarding;

public sealed record ChallengeData(string Email, Guid? UserId, long AccessVersion, string? Username=null,
    string? FullName=null, string? PasswordHash=null, string Channel="customer", string? NewEmail=null, string? GoogleSubject=null);
public sealed partial class OnboardingService(IdentityDbContext db,IdentityVault vault,IPasswordHasher<User> passwords,
    AuthenticationService authentication,IConfiguration configuration,AccountLifecycleGateway? lifecycle=null)
{
    public bool Enabled=>vault.Enabled;
    public void RequireEnabled(){if(!Enabled)throw new OnboardingException(503,"Verified onboarding is not enabled in this environment.");}
    public async Task<User> Account(Guid id)=>await db.Users.Include(x=>x.Roles).SingleOrDefaultAsync(x=>x.Id==id&&x.IsActive&&x.DeletedAt==null)
        ??throw new OnboardingException(401,"Your account is no longer active. Sign in again.");
    public async Task<bool> Available(string kind,string value,Guid? except=null)
    {
        var normalized=value.Trim().ToLowerInvariant();
        if(kind=="username")return Regex.IsMatch(normalized,@"^[a-z0-9_-]{3,50}$")&&!await db.Users.AnyAsync(x=>x.Id!=except&&x.Username.ToLower()==normalized);
        if(kind=="email")return normalized.Length<=100&&new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(normalized)&&!await db.Users.AnyAsync(x=>x.Id!=except&&x.Email.ToLower()==normalized);
        throw new OnboardingException(400,"Choose username or email availability.");
    }
    public void QueueEmail(TransactionalEmail email)
    {
        db.Operations.Add(new(){Id=email.EventId,UserId=email.UserId,Kind="Email",State="Queued",Ciphertext=vault.Encrypt(email,"email"),ExpiresAt=email.ExpiresAt,NextAttemptAt=DateTime.UtcNow});
    }
    public void Audit(Guid user,Guid? actor,string action,string reason)
    {
        db.Operations.Add(new(){UserId=user,Kind="Audit",State=action,Ciphertext=vault.Encrypt(new{actor,reason},"audit")});
    }
    public async Task<object> Signup(SignupDetails input)
    {
        RequireEnabled();
        if(!await Available("username",input.Username)||!await Available("email",input.Email))throw new OnboardingException(409,"The username or email is already in use.");
        var temporary=new User();var hash=passwords.HashPassword(temporary,input.Password);
        return await Challenge("Signup",new(input.Email.Trim(),null,0,input.Username.Trim(),input.FullName.Trim(),hash));
    }
    public async Task<object> Login(LoginRequest input)
    {
        var session=await authentication.LoginAsync(input);
        if(!Enabled)return session;
        var user=await Account(session.User.Id);
        if(user.NeedsInitialEmailOtp||user.EmailSecondStepEnabled)
            return await Challenge("Login",new(user.Email,user.Id,user.AccessVersion,Channel:input.LoginChannel));
        return session;
    }
    public async Task<object> Challenge(string kind,ChallengeData data)
    {
        RequireEnabled();
        if(configuration.GetValue<bool>("LOCAL_EMAIL_ALLOWLIST_REQUIRED")&&!string.IsNullOrWhiteSpace(configuration["EMAIL_ALLOWLIST"])&&
            !(configuration["EMAIL_ALLOWLIST"]??"").Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Contains(data.Email.Trim(),StringComparer.OrdinalIgnoreCase))
            throw new OnboardingException(422,"This local review sends codes only to configured test inboxes. Use an allowlisted email address.");
        var now=DateTime.UtcNow;var address=vault.Fingerprint("address",data.Email.Trim().ToLowerInvariant());
        await using var ownTransaction=db.Database.IsNpgsql()&&db.Database.CurrentTransaction==null?await db.Database.BeginTransactionAsync():null;
        if(db.Database.IsNpgsql())await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({address},0))");
        var recent=await db.Operations.Where(x=>x.AddressHash==address&&x.Kind.StartsWith("Otp:")&&x.CreatedAt>now.AddHours(-1)).OrderByDescending(x=>x.CreatedAt).ToListAsync();
        if(recent.Count>=5||recent.Any(x=>x.CreatedAt>now.AddSeconds(-60)))throw new OnboardingException(429,"Please wait before requesting another verification code.");
        var row=new IdentityOperation{UserId=data.UserId,Kind="Otp:"+kind,State="Pending",ExpiresAt=now.AddMinutes(10),AddressHash=address};
        var code=RandomNumberGenerator.GetInt32(0,1000000).ToString("D6",CultureInfo.InvariantCulture);
        row.Ciphertext=vault.Encrypt(data,row.Kind);row.CodeHash=vault.Fingerprint(row.Id.ToString(),code);
        db.Operations.Add(row);
        if(kind!="Recovery"||data.UserId.HasValue)QueueEmail(new(Guid.NewGuid(),data.UserId,data.Email,"Verification","Your FleetFlow verification code",
            $"{(string.IsNullOrWhiteSpace(data.FullName)?"Hello":$"Hi {data.FullName}")},\n\nYour verification code is {code}.\n\nIt expires in 10 minutes. If you did not request it, you can ignore this email.",ExpiresAt:row.ExpiresAt));
        await db.SaveChangesAsync();
        if(ownTransaction!=null)await ownTransaction.CommitAsync();
        return new{challengeId=row.Id,expiresAt=row.ExpiresAt,authenticationStatus="VerificationRequired",recoveryCodeAllowed=kind=="Login"&&data.UserId.HasValue&&!await db.Users.AnyAsync(x=>x.Id==data.UserId&&x.NeedsInitialEmailOtp),message="A verification code has been requested. Check your email."};
    }
    public async Task<object> Resend(Guid id)
    {
        RequireEnabled();var old=await db.Operations.SingleOrDefaultAsync(x=>x.Id==id&&x.Kind.StartsWith("Otp:"));
        if(old==null||old.State!="Pending"||old.ExpiresAt<=DateTime.UtcNow)throw new OnboardingException(400,"This request has expired. Start again.");
        var data=vault.Decrypt<ChallengeData>(old.Ciphertext,old.Kind);
        var version=old.Version;old.State="Superseded";old.Version=Guid.NewGuid();
        // Save together with the replacement; a failed cooldown does not consume the old code.
        try{return await Challenge(old.Kind[4..],data);}
        catch{old.State="Pending";old.Version=version;throw;}
    }
    public async Task<object> Verify(ChallengeInput input,string? newPassword=null,Guid? expectedUser=null,string? expectedPurpose=null)
    {
        RequireEnabled();await using var transaction=db.Database.IsNpgsql()?await db.Database.BeginTransactionAsync():null;
        if(db.Database.IsNpgsql())await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({input.ChallengeId.ToString()},0))");
        var row=await db.Operations.SingleOrDefaultAsync(x=>x.Id==input.ChallengeId&&x.Kind.StartsWith("Otp:"));
        if(row==null||row.State!="Pending"||row.ExpiresAt<=DateTime.UtcNow||row.Attempts>=5)throw new OnboardingException(400,"The code is invalid or expired. Start a new request.");
        if(expectedPurpose!=null&&row.Kind!="Otp:"+expectedPurpose)throw new OnboardingException(400,"This code cannot be used for that action.");
        var data=vault.Decrypt<ChallengeData>(row.Ciphertext,row.Kind);
        if(db.Database.IsNpgsql()&&data.UserId.HasValue)await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({data.UserId.Value.ToString()},0))");
        if(expectedUser.HasValue&&data.UserId!=expectedUser)throw new OnboardingException(403,"This challenge belongs to another account.");
        var validCode=!input.UseRecoveryCode&&Regex.IsMatch(input.Code??"",@"^[0-9]{6}$")&&vault.Matches(row.CodeHash,vault.Fingerprint(row.Id.ToString(),input.Code!));
        if(input.UseRecoveryCode&&row.Kind=="Otp:Login"&&data.UserId.HasValue)
        {
            var owner=await Account(data.UserId.Value);
            validCode=owner.EmailSecondStepEnabled&&!owner.NeedsInitialEmailOtp&&owner.AccessVersion==data.AccessVersion&&await ConsumeRecoveryCode(owner.Id,input.Code??"");
        }
        if(!validCode)
        {
            row.Attempts++;row.Version=Guid.NewGuid();if(row.Attempts>=5)row.State="Locked";
            await db.SaveChangesAsync();if(transaction!=null)await transaction.CommitAsync();
            throw new OnboardingException(400,"The code is invalid or expired.");
        }
        var kind=row.Kind[4..];User user;string[]? recoveryCodes=null;object? nextChallenge=null;
        if(kind is "Signup" or "GoogleSignup")
        {
            if(!await Available("username",data.Username!)||!await Available("email",data.Email))throw new OnboardingException(409,"The username or email was claimed while you were verifying. Start again.");
            var role=await db.Roles.SingleAsync(x=>x.Name=="CUSTOMER");
            user=new(){FullName=data.FullName!,Username=data.Username!,Email=data.Email,PasswordHash=data.PasswordHash!,EmailVerificationMethod="Otp"};user.Roles.Add(role);db.Users.Add(user);
            if(kind=="GoogleSignup"){user.HasLocalPassword=false;db.ExternalAccounts.Add(new(){UserId=user.Id,Subject=data.GoogleSubject!});}
            QueueEmail(new(Guid.NewGuid(),user.Id,user.Email,"Welcome","Welcome to FleetFlow",$"Hi {user.FullName},\n\nWelcome aboard. Your FleetFlow account is ready. Explore the fleet, save your plans and complete driving verification before your first booking.",ActionUrl:Frontend("#/customer-profile?section=driving")));
        }
        else
        {
            user=await Account(data.UserId??Guid.Empty);
            if(user.AccessVersion!=data.AccessVersion)throw new OnboardingException(400,"Your account changed. Start a new verification request.");
            if(kind=="Login"){user.EmailVerificationMethod="Otp";user.NeedsInitialEmailOtp=false;}
            else if(kind=="VerifyEmail"){user.EmailVerificationMethod="Otp";user.NeedsInitialEmailOtp=false;}
            else if(kind=="EmailChange")
            {
                if(!await Available("email",data.NewEmail!,user.Id))throw new OnboardingException(409,"That email address is already in use.");
                var old=user.Email;user.Email=data.NewEmail!;user.EmailVerificationMethod="Otp";user.AccessVersion++;
                QueueEmail(new(Guid.NewGuid(),user.Id,old,"Security","Your FleetFlow email changed","Your account email was changed. Contact FleetFlow if you did not request this change."));
            }
            else if(kind=="EmailChangeCurrent")
            {
                nextChallenge=await Challenge("EmailChange",data with{Email=data.NewEmail!});
            }
            else if(kind is "Recovery" or "SetPassword")
            {
                if(newPassword==null)throw new OnboardingException(400,"Enter a new password.");
                if(kind=="SetPassword"&&user.HasLocalPassword)throw new OnboardingException(409,"A password was already set. Start again.");
                user.PasswordHash=passwords.HashPassword(user,newPassword);user.HasLocalPassword=true;user.MustChangePassword=false;user.AccessVersion++;
                QueueEmail(new(Guid.NewGuid(),user.Id,user.Email,"Security","Your FleetFlow password changed","Your password was reset and previous sessions were revoked."));
            }
            else if(kind=="Enable2Step")
            {
                user.EmailSecondStepEnabled=true;user.AccessVersion++;recoveryCodes=await CreateRecoveryCodes(user.Id);
            }
            else if(kind=="Disable2Step"){user.EmailSecondStepEnabled=false;user.AccessVersion++;}
            else if(kind=="DeleteAccount")await DeleteVerified(user,row.Id);
            else throw new OnboardingException(400,"Unsupported verification purpose.");
        }
        row.State="Consumed";row.Version=Guid.NewGuid();row.CodeHash="";row.Ciphertext="";
        Audit(user.Id,user.Id,kind,"Email challenge completed.");await db.SaveChangesAsync();if(transaction!=null)await transaction.CommitAsync();
        if(nextChallenge!=null)return nextChallenge;
        if(kind=="DeleteAccount")return new{success=true,deleted=true,message="Your account is in the thirty-day administrator recycle bin."};
        if(kind is "Login" or "Signup" or "GoogleSignup")return authentication.CreateSession(user);
        if(kind is "Enable2Step" or "Disable2Step" or "EmailChange" or "SetPassword")return new{session=authentication.CreateSession(user),recoveryCodes,success=true};
        return new{success=true,message="Your change has been confirmed. Sign in again if your session was revoked."};
    }
    public string Frontend(string route)
    {
        var root=configuration["FRONTEND_PUBLIC_URL"]??"";
        if(!Uri.TryCreate(root,UriKind.Absolute,out var uri)||uri.Scheme is not ("http" or "https")||!string.IsNullOrEmpty(uri.UserInfo)||uri.AbsolutePath!="/"||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment))return "";
        if(uri.Scheme!="https"&&!uri.IsLoopback)return "";
        return root.TrimEnd('/')+"/"+route;
    }
    public async Task<object> Profile(Guid id)
    {
        var u=await Account(id);
        return new{u.Id,u.FullName,u.Username,u.Email,u.PhoneNumber,u.Address,u.ProfileImageUrl,u.EmailVerificationMethod,u.EmailSecondStepEnabled,u.HasLocalPassword};
    }
    public async Task<object> UpdateProfile(Guid id,ProfileDetails details)
    {
        RequireEnabled();var user=await Account(id);
        if(!await Available("username",details.Username,id))throw new OnboardingException(409,"That username is already in use.");
        if(details.PhoneNumber!=""&&!Regex.IsMatch(details.PhoneNumber,@"^\+[1-9][0-9]{6,14}$"))throw new OnboardingException(400,"Use an international phone number with its country code.");
        if(user.Username!=details.Username.Trim())user.AccessVersion++;
        user.Username=details.Username.Trim();user.FullName=details.FullName.Trim();user.PhoneNumber=details.PhoneNumber.Trim();user.Address=details.Address.Trim();
        Audit(id,id,"ProfileUpdated","Contact/profile details updated.");await db.SaveChangesAsync();return new{profile=await Profile(id),session=authentication.CreateSession(user)};
    }
    public async Task Reauthenticate(Guid id,string password,string? recoveryCode=null)
    {
        var user=await Account(id);
        if(!user.HasLocalPassword||passwords.VerifyHashedPassword(user,user.PasswordHash,password)==PasswordVerificationResult.Failed)throw new OnboardingException(400,"The current password is incorrect.");
        if(user.EmailSecondStepEnabled)
        {
            if(recoveryCode==null)throw new OnboardingException(400,"Enter a one-time recovery code to authorise this security change.");
            if(!await ConsumeRecoveryCode(id,recoveryCode))throw new OnboardingException(400,"The recovery code is invalid or already used.");
            await db.SaveChangesAsync();
        }
    }
}
