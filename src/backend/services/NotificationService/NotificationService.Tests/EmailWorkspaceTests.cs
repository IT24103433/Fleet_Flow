using System.Security.Cryptography;
using System.Text.Json;
using FleetFlow.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NotificationService.Api.Data;
using NotificationService.Api.Email;

namespace NotificationService.Tests;
public sealed class EmailWorkspaceTests
{
    [Fact]public void TemplatesEscapeUserContentAndRenderVerificationCodeWithoutUnsafeLinks()
    {
        var email=new TransactionalEmail(Guid.NewGuid(),null,"synthetic@example.test","Verification","<script>subject</script>","Hi <img src=x>,\nYour verification code is 123456.",ActionUrl:"javascript:alert(1)");
        var html=EmailPresentation.Render(email);Assert.DoesNotContain("<script>",html);Assert.DoesNotContain("<img src=x>",html);Assert.DoesNotContain("javascript:",html);Assert.Contains("123456",html);Assert.Contains("Valid for ten minutes",html);Assert.Contains("FleetFlow",html);
    }
    [Fact]public async Task EmailQueueDeduplicatesAndRejectsConflictingEventReuse()
    {
        using var db=new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["EMAIL_ENABLED"]="true",["EMAIL_DATA_KEY"]=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))}).Build();var vault=new EmailVault(config);var store=new EmailStore(db,vault);
        var email=new TransactionalEmail(Guid.NewGuid(),null,"synthetic@example.test","Welcome","Welcome","Hi Synthetic Driver");await store.Enqueue(email);await store.Enqueue(email);Assert.Single(db.EmailJobs);Assert.DoesNotContain("Synthetic Driver",db.EmailJobs.Single().Ciphertext);
        await Assert.ThrowsAsync<ArgumentException>(()=>store.Enqueue(email with{Message="Changed payload"}));
    }
    [Fact]public async Task WorkspaceCountsTheWholeQueueButReturnsOneMaskedPageWithoutCodes()
    {
        using var db=new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["EMAIL_ENABLED"]="true",["EMAIL_DATA_KEY"]=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))}).Build();var vault=new EmailVault(config);var store=new EmailStore(db,vault);
        for(var index=0;index<30;index++){var email=new TransactionalEmail(Guid.NewGuid(),null,"synthetic@example.test","Verification","Verify","Your verification code is 123456.");await store.Enqueue(email);}
        var result=Assert.IsType<OkObjectResult>(await new EmailController(store,vault,db).Workspace(page:2));var json=JsonSerializer.SerializeToElement(result.Value);Assert.Equal(30,json.GetProperty("totalCount").GetInt32());Assert.Equal(5,json.GetProperty("items").GetArrayLength());Assert.Equal(30,json.GetProperty("summary")[0].GetProperty("count").GetInt32());
        var output=json.ToString();Assert.DoesNotContain("123456",output);Assert.DoesNotContain("synthetic@example.test",output);Assert.Contains("s***@example.test",output);
        foreach(var row in db.EmailJobs){row.State="Sent";row.Ciphertext="";}await db.SaveChangesAsync();
        var sent=Assert.IsType<OkObjectResult>(await new EmailController(store,vault,db).Workspace(status:"Sent"));Assert.Contains("s***@example.test",JsonSerializer.Serialize(sent.Value));
    }
}
