using System.Net.Http.Json;
using FleetFlow.Contracts;
using FleetFlow.ServiceDefaults;
using IdentityService.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Api.Onboarding;

public sealed class IdentityEmailWorker(IServiceScopeFactory scopes,IdentityVault vault,IHttpClientFactory clients,IServiceTokenProvider tokens):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if(!vault.Enabled)return;
        while(!ct.IsCancellationRequested)
        {
            try
            {
                using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                var rows=await db.Operations.FromSqlRaw("""
                    UPDATE "IdentityOperations" SET "State"='Sending',"NextAttemptAt"=NOW()+INTERVAL '30 seconds',"Attempts"="Attempts"+1,"Version"=gen_random_uuid()
                    WHERE "Id"=(SELECT "Id" FROM "IdentityOperations" WHERE "Kind"='Email' AND "State" IN ('Queued','Sending') AND "NextAttemptAt"<=NOW()
                        ORDER BY "CreatedAt" FOR UPDATE SKIP LOCKED LIMIT 1) RETURNING *
                    """).ToListAsync(ct);
                var row=rows.SingleOrDefault();if(row==null){await Task.Delay(1000,ct);continue;}
                if(row.ExpiresAt<=DateTime.UtcNow){row.State="Expired";row.Ciphertext="";await db.SaveChangesAsync(ct);continue;}
                try
                {
                    using var request=new HttpRequestMessage(HttpMethod.Post,"internal/email"){Content=JsonContent.Create(vault.Decrypt<TransactionalEmail>(row.Ciphertext,"email"))};
                    request.Headers.Authorization=new("Bearer",await tokens.GetTokenAsync(ct));
                    using var response=await clients.CreateClient("OnboardingNotification").SendAsync(request,ct);response.EnsureSuccessStatusCode();
                    row.State="Delivered";row.Ciphertext="";
                }
                catch(Exception) when(!ct.IsCancellationRequested){row.State=row.Attempts>=8?"Failed":"Queued";row.NextAttemptAt=DateTime.UtcNow.AddSeconds(Math.Min(300,Math.Pow(2,row.Attempts)*5));}
                await db.SaveChangesAsync(ct);
            }
            catch(Exception) when(!ct.IsCancellationRequested){await Task.Delay(2000,ct);}
        }
    }
}
