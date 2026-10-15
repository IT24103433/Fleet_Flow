using System.ComponentModel.DataAnnotations;
using FleetFlow.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace NotificationService.Api.Email;

public sealed class SupportInput
{
    public Guid RequestId {get;set;}
    [Required,StringLength(100,MinimumLength=2)]public string Name {get;set;}="";
    [Required,EmailAddress,StringLength(100)]public string Email {get;set;}="";
    [Required,StringLength(80)]public string Topic {get;set;}="";
    [Required,StringLength(3000,MinimumLength=10)]public string Message {get;set;}="";
    [StringLength(200)]public string Website {get;set;}="";
}
[ApiController,Route("api/support"),AllowAnonymous,EnableRateLimiting("support")]
public sealed class SupportController(EmailStore store,EmailVault vault,IConfiguration configuration):ControllerBase
{
    [HttpPost,RequestSizeLimit(16384)]
    public async Task<IActionResult> Create(SupportInput input)
    {
        if(!vault.Enabled)return StatusCode(503,new{message="Enquiries are temporarily unavailable. Please try again later."});
        if(input.Website!="")return Accepted(new{success=true});
        if(input.RequestId==Guid.Empty||input.Topic is not ("Booking" or "Driver verification" or "Account" or "Other"))return BadRequest(new{message="Choose a topic and provide a request reference."});
        var inbox=configuration["SUPPORT_INBOX"]??configuration["SMTP_USERNAME"]??"";
        if(string.IsNullOrWhiteSpace(inbox))return StatusCode(503,new{message="Enquiries are temporarily unavailable."});
        try
        {
            await store.Enqueue(new(input.RequestId,null,inbox,"Support",$"FleetFlow enquiry: {input.Topic}",
                $"Enquiry reference: {input.RequestId}\nFrom: {input.Name.Trim()}\nTopic: {input.Topic}\n\n{input.Message.Trim()}",ReplyTo:input.Email.Trim()));
            return Accepted(new{success=true,reference=input.RequestId,message="Your enquiry has been saved. FleetFlow can reply to your email address."});
        }
        catch(ArgumentException){return BadRequest(new{message="Check your enquiry details. A request reference cannot be reused for different content."});}
    }
}
