using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using IdentityService.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Api.Onboarding;

public sealed partial class OnboardingService
{
    private async Task ConfirmPassword(Guid id,string password)
    {
        var user=await Account(id);
        if(!user.HasLocalPassword||passwords.VerifyHashedPassword(user,user.PasswordHash,password)==PasswordVerificationResult.Failed)
            throw new OnboardingException(400,"The current password is incorrect. Google-only accounts can set a password through email recovery first.");
    }
    private async Task ConfirmSecurity(Guid id,SecurityInput input)
    {
        var user=await Account(id);
        if(user.HasLocalPassword){await ConfirmPassword(id,input.Password);return;}
        var grant=await db.Operations.SingleOrDefaultAsync(x=>x.Id==input.ReauthenticationId&&x.UserId==id&&x.Kind=="GoogleSecurityGrant");
        if(grant==null||grant.State!="Available"||grant.ExpiresAt<=DateTime.UtcNow||vault.Decrypt<long>(grant.Ciphertext,"GoogleSecurityGrant")!=user.AccessVersion)
            throw new OnboardingException(401,"Confirm your linked Google account again before making this change.");
        grant.State="Consumed";grant.Ciphertext="";grant.Version=Guid.NewGuid();
    }
    public async Task<object> RequestRecovery(RecoverInput input)
    {
        RequireEnabled();var email=(input.Email??"").Trim();
        if(email.Length>100||!new EmailAddressAttribute().IsValid(email))throw new OnboardingException(400,"Enter a valid email address.");
        var user=await db.Users.SingleOrDefaultAsync(x=>x.Email.ToLower()==email.ToLower()&&x.IsActive&&x.DeletedAt==null);
        var result=await Challenge("Recovery",new(user?.Email??email,user?.Id,user?.AccessVersion??0));
        // Challenge shape and public copy are shared with unknown accounts; never disclose account existence.
        return result;
    }
    public async Task<object> RequestEmailChange(Guid id,SecurityInput input)
    {
        RequireEnabled();await using var tx=db.Database.IsNpgsql()?await db.Database.BeginTransactionAsync():null;var user=await Account(id);
        var email=(input.NewEmail??"").Trim();
        if(email.Length>100||!new EmailAddressAttribute().IsValid(email))throw new OnboardingException(400,"Enter a valid new email address.");
        if(email.Equals(user.Email,StringComparison.OrdinalIgnoreCase))throw new OnboardingException(400,"Enter a different email address.");
        if(!await Available("email",email,id))throw new OnboardingException(409,"That email address is already in use.");
        await ConfirmSecurity(id,input);
        var result=await Challenge(user.EmailSecondStepEnabled?"EmailChangeCurrent":"EmailChange",
            new(user.EmailSecondStepEnabled?user.Email:email,id,user.AccessVersion,NewEmail:email));
        if(tx!=null)await tx.CommitAsync();return result;
    }
    public async Task<object> RequestSetPassword(Guid id,SecurityInput input)
    {
        RequireEnabled();await using var tx=db.Database.IsNpgsql()?await db.Database.BeginTransactionAsync():null;var user=await Account(id);
        if(user.HasLocalPassword)throw new OnboardingException(409,"Your account already has a password. Use change password instead.");
        await ConfirmSecurity(id,input);
        var result=await Challenge("SetPassword",new(user.Email,id,user.AccessVersion));if(tx!=null)await tx.CommitAsync();return result;
    }
    public async Task<object> RequestSecondStep(Guid id,bool enable,SecurityInput input)
    {
        RequireEnabled();await using var tx=db.Database.IsNpgsql()?await db.Database.BeginTransactionAsync():null;var user=await Account(id);
        if(user.EmailSecondStepEnabled==enable)throw new OnboardingException(409,"That security setting is already active.");
        await ConfirmSecurity(id,input);
        var result=await Challenge(enable?"Enable2Step":"Disable2Step",new(user.Email,id,user.AccessVersion));if(tx!=null)await tx.CommitAsync();return result;
    }
    private async Task<bool> ConsumeRecoveryCode(Guid user,string code)
    {
        var normalized=Regex.Replace(code.Trim().ToUpperInvariant(),@"[\s-]","");
        if(!Regex.IsMatch(normalized,@"^[A-F0-9]{16}$"))return false;
        var hash=vault.Fingerprint("recovery:"+user,normalized);
        var row=await db.Operations.SingleOrDefaultAsync(x=>x.UserId==user&&x.Kind=="RecoveryCode"&&x.CodeHash==hash&&x.State=="Available");
        if(row==null)return false;row.State="Used";row.CodeHash="";row.Version=Guid.NewGuid();return true;
    }
    private async Task<string[]> CreateRecoveryCodes(Guid user)
    {
        foreach(var old in await db.Operations.Where(x=>x.UserId==user&&x.Kind=="RecoveryCode"&&x.State=="Available").ToListAsync())
        {old.State="Revoked";old.CodeHash="";old.Version=Guid.NewGuid();}
        return Enumerable.Range(0,8).Select(_=>{
            var raw=Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
            db.Operations.Add(new(){UserId=user,Kind="RecoveryCode",State="Available",CodeHash=vault.Fingerprint("recovery:"+user,raw)});
            return string.Join("-",Enumerable.Range(0,4).Select(i=>raw.Substring(i*4,4)));
        }).ToArray();
    }
}
