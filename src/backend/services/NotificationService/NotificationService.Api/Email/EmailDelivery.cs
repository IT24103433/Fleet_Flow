using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FleetFlow.Contracts;
using FleetFlow.ServiceDefaults;
using Microsoft.EntityFrameworkCore;
using NotificationService.Api.Data;

namespace NotificationService.Api.Email;

public sealed class EmailJob
{
    public Guid Id {get;set;}
    public Guid? UserId {get;set;}
    public string Ciphertext {get;set;}="";
    public string PayloadHash {get;set;}="";
    public string Template {get;set;}="";
    public string RecipientHint {get;set;}="";
    public string State {get;set;}="Queued";
    public string ErrorCode {get;set;}="";
    public int Attempts {get;set;}
    public DateTime CreatedAt {get;set;}=DateTime.UtcNow;
    public DateTime NextAttemptAt {get;set;}=DateTime.UtcNow;
    public DateTime? ExpiresAt {get;set;}
    public DateTime? SentAt {get;set;}
}
public sealed class EmailVault(IConfiguration configuration)
{
    public bool Enabled=>configuration.GetValue<bool>("EMAIL_ENABLED");
    public bool DeliveryConfigured=>Enabled&&!string.IsNullOrWhiteSpace(configuration["SMTP_PASSWORD"])&&!string.IsNullOrWhiteSpace(configuration["SMTP_USERNAME"]);
    private byte[] Key(){var key=Convert.FromBase64String(configuration["EMAIL_DATA_KEY"]??"");if(key.Length!=32)throw new InvalidOperationException("Configure EMAIL_DATA_KEY as a private base64-encoded 32-byte key.");return key;}
    public void Validate(){if(Enabled)_=Key();}
    public string Protect(TransactionalEmail email)
    {
        var plain=JsonSerializer.SerializeToUtf8Bytes(email);var nonce=RandomNumberGenerator.GetBytes(12);var cipher=new byte[plain.Length];var tag=new byte[16];
        using var aes=new AesGcm(Key(),16);aes.Encrypt(nonce,plain,cipher,tag,Encoding.UTF8.GetBytes(email.EventId.ToString()));
        return Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }
    public TransactionalEmail Read(EmailJob row)
    {
        var bytes=Convert.FromBase64String(row.Ciphertext);var plain=new byte[bytes.Length-28];using var aes=new AesGcm(Key(),16);
        aes.Decrypt(bytes.AsSpan(0,12),bytes.AsSpan(28),bytes.AsSpan(12,16),plain,Encoding.UTF8.GetBytes(row.Id.ToString()));
        return JsonSerializer.Deserialize<TransactionalEmail>(plain)!;
    }
}
public sealed class EmailStore(NotificationDbContext db,EmailVault vault)
{
    public static string MaskRecipient(string address){var at=address.IndexOf('@');return at>0?address[..1]+"***"+address[at..]:"";}
    public async Task Enqueue(TransactionalEmail email)
    {
        if(!vault.Enabled)throw new InvalidOperationException("Transactional email is not configured.");
        if(email.EventId==Guid.Empty||email.Subject.Length is <1 or >180||email.Message.Length>12000||email.Recipient.Length>100||email.Subject.Contains('\r')||email.Subject.Contains('\n'))throw new ArgumentException("Invalid email request.");
        if(email.Recipient!=""&&!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email.Recipient))throw new ArgumentException("Invalid recipient.");
        if(email.ReplyTo!=null&&!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email.ReplyTo))throw new ArgumentException("Invalid reply address.");
        var hash=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(email)));
        var previous=await db.EmailJobs.SingleOrDefaultAsync(x=>x.Id==email.EventId);
        if(previous!=null){if(previous.PayloadHash!=hash)throw new ArgumentException("Event identity was reused for a different email.");return;}
        db.EmailJobs.Add(new(){Id=email.EventId,UserId=email.UserId,Ciphertext=vault.Protect(email),PayloadHash=hash,Template=email.Template,RecipientHint=MaskRecipient(email.Recipient),ExpiresAt=email.ExpiresAt});
        try{await db.SaveChangesAsync();}
        catch(DbUpdateException ex) when(ex.InnerException is Npgsql.PostgresException{SqlState:"23505"})
        {db.ChangeTracker.Clear();previous=await db.EmailJobs.SingleAsync(x=>x.Id==email.EventId);if(previous.PayloadHash!=hash)throw new ArgumentException("Conflicting email identity.");}
    }
}
public sealed class EmailWorker(IServiceScopeFactory scopes,EmailVault vault,IConfiguration configuration,IHostEnvironment environment,
    IHttpClientFactory clients,IServiceTokenProvider tokens):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if(!vault.Enabled)return;vault.Validate();
        using(var initial=scopes.CreateScope())
        {
            var db=initial.ServiceProvider.GetRequiredService<NotificationDbContext>();
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "EmailJobs" (
                    "Id" uuid PRIMARY KEY,"UserId" uuid NULL,"Ciphertext" text NOT NULL,"PayloadHash" text NOT NULL,
                    "Template" text NOT NULL,"State" text NOT NULL,"ErrorCode" text NOT NULL,"Attempts" integer NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,"NextAttemptAt" timestamp with time zone NOT NULL,
                    "ExpiresAt" timestamp with time zone NULL,"SentAt" timestamp with time zone NULL);
                CREATE INDEX IF NOT EXISTS "IX_EmailJobs_State_NextAttemptAt" ON "EmailJobs" ("State","NextAttemptAt");
                ALTER TABLE "EmailJobs" ADD COLUMN IF NOT EXISTS "RecipientHint" text NOT NULL DEFAULT '';
                """,ct);
        }
        while(!ct.IsCancellationRequested)
        {
            try
            {
                using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
                var rows=await db.EmailJobs.FromSqlRaw("""
                    UPDATE "EmailJobs" SET "State"='Sending',"NextAttemptAt"=NOW()+INTERVAL '90 seconds',"Attempts"="Attempts"+1
                    WHERE "Id"=(SELECT "Id" FROM "EmailJobs" WHERE "State" IN ('Queued','Sending') AND "NextAttemptAt"<=NOW()
                        ORDER BY "CreatedAt" FOR UPDATE SKIP LOCKED LIMIT 1) RETURNING *
                    """).ToListAsync(ct);
                var row=rows.SingleOrDefault();
                if(row==null){await Task.Delay(1000,ct);continue;}
                if(row.ExpiresAt<=DateTime.UtcNow){row.State="Expired";row.Ciphertext="";await db.SaveChangesAsync(ct);continue;}
                try
                {
                    var email=vault.Read(row);
                    if(email.Recipient==""&&email.UserId is {} user)
                    {
                        using var request=new HttpRequestMessage(HttpMethod.Get,$"internal/notification-contact/{user}");
                        request.Headers.Authorization=new("Bearer",await tokens.GetTokenAsync(ct));
                        using var response=await clients.CreateClient("EmailIdentity").SendAsync(request,ct);
                        if(response.StatusCode==HttpStatusCode.NotFound){row.State="Skipped";row.Ciphertext="";await db.SaveChangesAsync(ct);continue;}
                        response.EnsureSuccessStatusCode();var contact=await response.Content.ReadFromJsonAsync<Contact>(ct);
                        email=email with{Recipient=contact?.Email??throw new InvalidOperationException()};
                    }
                    row.RecipientHint=EmailStore.MaskRecipient(email.Recipient);
                    if(!vault.DeliveryConfigured)throw new EmailFailure("ConfigurationMissing");
                    if(environment.IsDevelopment())
                    {
                        var allow=(configuration["EMAIL_ALLOWLIST"]??"").Split(',',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
                        if(!allow.Contains(email.Recipient,StringComparer.OrdinalIgnoreCase))throw new EmailFailure("RecipientNotAllowlisted");
                    }
                    using var message=new MailMessage{From=new MailAddress(configuration["SMTP_USERNAME"]!,"FleetFlow Automations"),Subject=email.Subject};
                    message.To.Add(email.Recipient);if(email.ReplyTo!=null)message.ReplyToList.Add(new(email.ReplyTo));
                    message.Headers.Add("Message-ID",$"<{email.EventId:N}@fleetflow.local>");
                    var html=EmailPresentation.Render(email);
                    message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(email.Message+(email.ActionUrl==null?"":"\n"+email.ActionUrl),Encoding.UTF8,"text/plain"));
                    message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html,Encoding.UTF8,"text/html"));
                    using var smtp=new SmtpClient("smtp.gmail.com",587){EnableSsl=true,Credentials=new NetworkCredential(configuration["SMTP_USERNAME"],configuration["SMTP_PASSWORD"])};
                    using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(40));
                    await smtp.SendMailAsync(message,timeout.Token);row.State="Sent";row.SentAt=DateTime.UtcNow;row.ErrorCode="";
                    if(email.Template=="Verification")row.Ciphertext="";
                }
                catch(Exception ex) when(ex is not OperationCanceledException||!ct.IsCancellationRequested)
                {row.ErrorCode=ex is EmailFailure f?f.Code:"DeliveryFailed";row.State=row.Attempts>=5?"Failed":"Queued";row.NextAttemptAt=DateTime.UtcNow.AddSeconds(Math.Min(300,Math.Pow(2,row.Attempts)*5));}
                await db.SaveChangesAsync(ct);
            }
            catch(Exception) when(!ct.IsCancellationRequested){await Task.Delay(3000,ct);}
        }
    }
    private sealed record Contact(string Email);
    private sealed class EmailFailure(string code):Exception{public string Code=>code;}
}
