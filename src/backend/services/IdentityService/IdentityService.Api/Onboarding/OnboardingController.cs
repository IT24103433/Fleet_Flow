using System.Security.Claims;
using FleetFlow.Contracts;
using IdentityService.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Api.Onboarding;

[ApiController,Route("api/onboarding"),EnableRateLimiting("onboarding")]
public sealed class OnboardingController(OnboardingService service,DriverVerificationService drivers,IdentityDbContext db,IConfiguration config,GoogleAuthentication google):ControllerBase
{
    private Guid Id=>Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)?id:throw new OnboardingException(401,"Sign in again.");
    private async Task<IActionResult> Run(Func<Task<object>> action)
    {
        try{return Ok(await action());}
        catch(OnboardingException ex){return StatusCode(ex.Status,new{message=ex.Message});}
        catch(DbUpdateConcurrencyException){return Conflict(new{message="This record changed. Refresh before trying again."});}
        catch(DbUpdateException ex) when(ex.InnerException is Npgsql.PostgresException{SqlState:"23505"}){return Conflict(new{message="An account or document identifier has already been claimed. Refresh and try again."});}
    }
    [AllowAnonymous,HttpGet("config")]
    public IActionResult Configuration()=>Ok(new{enabled=service.Enabled,googleClientId=service.Enabled?config["GOOGLE_CLIENT_ID"]:null});
    [AllowAnonymous,HttpGet("availability")]
    public Task<IActionResult> Availability(string kind,string value)=>Run(async()=>{service.RequireEnabled();return new{available=await service.Available(kind,value)};});
    [AllowAnonymous,HttpPost("signup")]
    public Task<IActionResult> Signup(SignupDetails input)=>Run(()=>service.Signup(input));
    [AllowAnonymous,HttpPost("verify")]
    public Task<IActionResult> Verify(ChallengeInput input)=>Run(()=>service.Verify(input));
    [AllowAnonymous,HttpPost("resend/{id:guid}")]
    public Task<IActionResult> Resend(Guid id)=>Run(()=>service.Resend(id));
    [AllowAnonymous,HttpPost("recovery")]
    public Task<IActionResult> Recovery(RecoverInput input)=>Run(()=>service.RequestRecovery(input));
    [AllowAnonymous,HttpPost("recovery/reset")]
    public Task<IActionResult> Reset(ResetInput input)=>Run(()=>service.Verify(new(input.ChallengeId,input.Code),input.NewPassword,expectedPurpose:"Recovery"));
    [Authorize,HttpPost("email-change")]
    public Task<IActionResult> ChangeEmail(SecurityInput input)=>Run(()=>service.RequestEmailChange(Id,input));
    [Authorize,HttpPost("second-step/{enable:bool}")]
    public Task<IActionResult> SecondStep(bool enable,SecurityInput input)=>Run(()=>service.RequestSecondStep(Id,enable,input));
    [Authorize,HttpPost("deletion")]
    public Task<IActionResult> DeleteRequest()=>Run(()=>service.RequestDeletion(Id));
    [Authorize,HttpPost("deletion/confirm")]
    public Task<IActionResult> DeleteConfirm(ChallengeInput input)=>Run(()=>service.Verify(input,expectedUser:Id,expectedPurpose:"DeleteAccount"));
    [Authorize(Roles="ADMIN"),HttpGet("recycle-bin")]
    public Task<IActionResult> Recycle()=>Run(()=>service.RecycleBin());
    [Authorize(Roles="ADMIN"),HttpPost("recycle-bin/{account:guid}/restore")]
    public Task<IActionResult> Restore(Guid account,ReviewInput input)=>Run(()=>service.RestoreAccount(Id,account,input.Reason));
    [AllowAnonymous,HttpPost("google/begin")]
    public Task<IActionResult> GoogleBegin()=>Run(()=>google.Begin(null));
    [AllowAnonymous,HttpPost("google/complete")]
    public Task<IActionResult> GoogleComplete(GoogleInput input)=>Run(()=>google.Complete(input,null,HttpContext.RequestAborted));
    [AllowAnonymous,HttpPost("google/signup/complete")]
    public Task<IActionResult> GoogleSignup(GoogleSignupInput input)=>Run(()=>google.FinishSignup(input,HttpContext.RequestAborted));
    [Authorize,HttpPost("google/security/begin")]
    public Task<IActionResult> GoogleSecurityBegin()=>Run(()=>google.Begin(Id,true));
    [Authorize,HttpPost("google/security/complete")]
    public Task<IActionResult> GoogleSecurityComplete(GoogleInput input)=>Run(()=>google.ConfirmSecurity(input,Id,HttpContext.RequestAborted));
    [Authorize,HttpPost("password/set")]
    public Task<IActionResult> SetPasswordRequest(SecurityInput input)=>Run(()=>service.RequestSetPassword(Id,input));
    [Authorize,HttpPost("password/set/confirm")]
    public Task<IActionResult> SetPasswordConfirm(ResetInput input)=>Run(()=>service.Verify(new(input.ChallengeId,input.Code),input.NewPassword,Id,"SetPassword"));
    [Authorize,HttpPost("google/link/begin")]
    public Task<IActionResult> GoogleLinkBegin()=>Run(()=>google.Begin(Id));
    [Authorize,HttpPost("google/link/complete")]
    public Task<IActionResult> GoogleLink(GoogleInput input)=>Run(()=>google.Complete(input,Id,HttpContext.RequestAborted));
    [Authorize,HttpGet("profile")]
    public Task<IActionResult> Profile()=>Run(()=>service.Profile(Id));
    [Authorize,HttpPut("profile")]
    public Task<IActionResult> Profile(ProfileDetails input)=>Run(()=>service.UpdateProfile(Id,input));
    [Authorize,HttpPost("verify-email")]
    public Task<IActionResult> VerifyEmail()=>Run(async()=>{var user=await service.Account(Id);return await service.Challenge("VerifyEmail",new(user.Email,user.Id,user.AccessVersion));});
    [Authorize,HttpGet("driver")]
    public Task<IActionResult> Driver()=>Run(()=>drivers.Get(Id));
    [Authorize,HttpGet("eligibility")]
    public Task<IActionResult> Eligibility(DateTime returnAt)=>Run(async()=>await drivers.Eligibility(Id,returnAt));
    [Authorize,HttpPut("driver/draft"),RequestSizeLimit(65536)]
    public Task<IActionResult> Draft(DriverDetails details)=>Run(()=>drivers.Save(Id,details,false));
    [Authorize,HttpPost("driver/submit"),RequestSizeLimit(65536)]
    public Task<IActionResult> Submit(DriverDetails details)=>Run(()=>drivers.Save(Id,details,true));
    [Authorize,HttpPost("driver/photos/{kind}"),Consumes("multipart/form-data"),RequestSizeLimit(6*1024*1024)]
    public Task<IActionResult> Upload(string kind,IFormFile file)=>Run(()=>drivers.Upload(Id,kind,file));
    [Authorize,HttpDelete("driver/photos/{photo:guid}")]
    public Task<IActionResult> Remove(Guid photo)=>Run(()=>drivers.RemovePhoto(Id,photo));
    [Authorize,HttpGet("driver/photos/{photo:guid}")]
    public async Task<IActionResult> Photo(Guid photo)
    {
        try{var data=await drivers.Photo(Id,photo,User.IsInRole("ADMIN"));Response.Headers.CacheControl="no-store";Response.Headers["X-Content-Type-Options"]="nosniff";return File(data.Bytes,data.Type);}
        catch(OnboardingException ex){return StatusCode(ex.Status,new{message=ex.Message});}
    }
    [Authorize(Roles="ADMIN"),HttpGet("reviews")]
    public Task<IActionResult> Queue()=>Run(()=>drivers.Queue());
    [Authorize(Roles="ADMIN"),HttpGet("accounts/email-status")]
    public Task<IActionResult> EmailStatus()=>Run(async()=>{service.RequireEnabled();return await db.Users.AsNoTracking().Where(x=>x.DeletedAt==null)
        .OrderBy(x=>x.FullName).Select(x=>new{x.Id,x.FullName,x.Username,x.Email,x.IsActive,x.EmailVerificationMethod,x.NeedsInitialEmailOtp}).ToListAsync();});
    [Authorize(Roles="ADMIN"),HttpPost("reviews/{revision:guid}")]
    public Task<IActionResult> Review(Guid revision,ReviewInput input)=>Run(()=>drivers.Review(Id,revision,input));
    [Authorize(Roles="ADMIN"),HttpPost("email-waiver/{account:guid}")]
    public Task<IActionResult> Waive(Guid account,[FromBody] ReviewInput input)=>Run(async()=>{
        service.RequireEnabled();if(string.IsNullOrWhiteSpace(input.Reason)||input.Reason.Length>500)throw new OnboardingException(400,"Record a reason for waiving initial email verification.");
        var user=await service.Account(account);if(user.EmailVerificationMethod!="Unverified"&&!user.NeedsInitialEmailOtp)throw new OnboardingException(409,"This account is already email verified or approved by an administrator.");
        user.EmailVerificationMethod="AdminWaived";user.NeedsInitialEmailOtp=false;
        service.Audit(account,Id,"InitialEmailWaiver",input.Reason);await db.SaveChangesAsync();return new{success=true};
    });
}

[ApiController,Route("internal")]
public sealed class OnboardingInternalController(DriverVerificationService drivers,IdentityDbContext db):ControllerBase
{
    [HttpGet("driver-eligibility/{id:guid}"),Authorize(Policy=OnboardingScopes.DriverEligibility)]
    public async Task<IActionResult> Eligibility(Guid id,DateTime returnAt)
    {try{return Ok(await drivers.Eligibility(id,returnAt));}catch(OnboardingException ex){return StatusCode(ex.Status,new{message=ex.Message});}}
    [HttpGet("notification-contact/{id:guid}"),Authorize(Policy=OnboardingScopes.ContactRead)]
    public async Task<IActionResult> Contact(Guid id)
    {var account=await db.Users.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id&&x.IsActive&&x.DeletedAt==null);return account==null?NotFound():Ok(new{account.Email});}
}
