using FleetFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Api.Data;

namespace NotificationService.Api.Email;

[ApiController]
public sealed class EmailController(EmailStore store,EmailVault vault,NotificationDbContext db):ControllerBase
{
    [HttpPost("internal/email"),Authorize(Policy=OnboardingScopes.EmailEnqueue)]
    public async Task<IActionResult> Enqueue(TransactionalEmail request)
    {
        if(!vault.Enabled)return StatusCode(503,new{message="Email delivery is not enabled."});
        try{await store.Enqueue(request);return Accepted(new{eventId=request.EventId});}
        catch(ArgumentException){return BadRequest(new{message="Invalid or conflicting email request."});}
    }
    [HttpGet("api/email-deliveries"),Authorize(Roles="ADMIN")]
    public async Task<IActionResult> Deliveries()=>Ok(await db.EmailJobs.AsNoTracking().OrderByDescending(x=>x.CreatedAt).Take(100)
        .Select(x=>new{x.Id,x.UserId,x.Template,x.State,x.ErrorCode,x.Attempts,x.CreatedAt,x.SentAt,x.ExpiresAt}).ToListAsync());
    [HttpGet("api/email-deliveries/workspace"),Authorize(Roles="ADMIN")]
    public async Task<IActionResult> Workspace(string status="",string template="",int page=1)
    {
        if(page<1||page>1000000||status.Length>30||template.Length>50)return BadRequest(new{message="Choose a valid message filter or page."});
        var counts=await db.EmailJobs.AsNoTracking().GroupBy(x=>x.State).Select(g=>new{state=g.Key,count=g.Count()}).ToListAsync();
        var query=db.EmailJobs.AsNoTracking();if(status=="Queued")query=query.Where(x=>x.State=="Queued"||x.State=="Sending");else if(status!="")query=query.Where(x=>x.State==status);if(template!="")query=query.Where(x=>x.Template==template);
        var total=await query.CountAsync();var records=await query.OrderByDescending(x=>x.CreatedAt).ThenBy(x=>x.Id).Skip((page-1)*25).Take(25).ToListAsync();
        var rows=records.Select(row=>{
            var recipient=row.RecipientHint;
            if(row.Ciphertext!="")try{var address=vault.Read(row).Recipient;var at=address.IndexOf('@');if(at>0)recipient=address[..1]+"***"+address[at..];}catch(System.Security.Cryptography.CryptographicException){}catch(FormatException){}catch(System.Text.Json.JsonException){}
            return new{row.Id,row.UserId,row.Template,row.State,row.ErrorCode,row.Attempts,row.CreatedAt,row.SentAt,row.ExpiresAt,recipientMasked=recipient};
        }).ToList();
        return Ok(new{items=rows,totalCount=total,page,totalPages=(int)Math.Ceiling(total/25m),summary=counts});
    }
    [HttpPost("api/email-deliveries/{id:guid}/replay"),Authorize(Roles="ADMIN")]
    public async Task<IActionResult> Replay(Guid id)
    {
        var row=await db.EmailJobs.SingleOrDefaultAsync(x=>x.Id==id);if(row==null)return NotFound();
        if(row.State!="Failed"||row.ExpiresAt<=DateTime.UtcNow||row.Ciphertext=="")return Conflict(new{message="Only failed, unexpired messages can be retried."});
        row.State="Queued";row.NextAttemptAt=DateTime.UtcNow;row.Attempts=0;
        try{await db.SaveChangesAsync();return Ok(new{success=true});}
        catch(DbUpdateConcurrencyException){return Conflict(new{message="Delivery state changed. Refresh before retrying."});}
    }
    [HttpPost("internal/accounts/{user:guid}/anonymize"),Authorize(Policy=OnboardingScopes.NotificationLifecycle)]
    public async Task<IActionResult> Anonymize(Guid user,AccountLifecycleCommand input)
    {
        if(input.OperationId==Guid.Empty)return BadRequest();
        await using var tx=db.Database.IsRelational()?await db.Database.BeginTransactionAsync():null;
        // Keep event IDs/read states and history references while removing personal message payloads.
        foreach(var message in await db.Notifications.Where(x=>x.TargetUserId==user).ToListAsync())
        {message.Title="Account removed";message.Message="Personal notification content has been anonymised.";}
        foreach(var email in await db.EmailJobs.Where(x=>x.UserId==user).ToListAsync())
        {email.Ciphertext="";email.PayloadHash="";email.RecipientHint="";email.State="Anonymized";email.ErrorCode="";}
        await db.SaveChangesAsync();if(tx!=null)await tx.CommitAsync();return Ok(new{success=true});
    }
}
