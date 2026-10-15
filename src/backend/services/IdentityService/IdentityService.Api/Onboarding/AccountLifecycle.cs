using IdentityService.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Api.Onboarding;

public sealed partial class OnboardingService
{
    public async Task<object> RequestDeletion(Guid id)
    {
        RequireEnabled();var user=await Account(id);CustomerOnly(user);
        if(lifecycle==null||await lifecycle.Outstanding(id))throw new OnboardingException(409,"Resolve future bookings or unfinished rentals before requesting account deletion.");
        return await Challenge("DeleteAccount",new(user.Email,user.Id,user.AccessVersion));
    }
    private static void CustomerOnly(User user)
    {if(user.Roles.Count!=1||!user.Roles.Any(r=>r.Name=="CUSTOMER"))throw new OnboardingException(403,"Self-service deletion is available only to customer accounts.");}
    private async Task DeleteVerified(User user,Guid operation)
    {
        CustomerOnly(user);
        if(lifecycle==null)throw new OnboardingException(503,"Account deletion is not configured.");
        // Rental freezes new booking creation under its customer lock before Identity revokes access.
        operation=new Guid(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("delete:"+operation)).AsSpan(0,16));
        await lifecycle.Change("OnboardingRental",user.Id,"freeze",operation);
        user.DeletedAt=DateTime.UtcNow;user.IsActive=false;user.AccessVersion++;
        db.Operations.Add(new(){Id=operation,UserId=user.Id,Kind="AccountDeletion",State="Recycled",ExpiresAt=user.DeletedAt.Value.AddDays(30),
            Ciphertext=vault.Encrypt(new{operation},"AccountDeletion")});
        QueueEmail(new(Guid.NewGuid(),user.Id,user.Email,"Deletion","Your FleetFlow deletion request is confirmed",
            $"Your access has been revoked. Your account is in the administrator recycle bin until {user.DeletedAt.Value.AddDays(30):yyyy-MM-dd HH:mm} UTC. After that time, personal data will be anonymised while necessary rental history is retained."));
    }
    public async Task<object> RecycleBin()
    {
        RequireEnabled();var rows=await db.Users.AsNoTracking().Where(x=>x.DeletedAt!=null&&x.EmailVerificationMethod!="Anonymized").OrderByDescending(x=>x.DeletedAt).ToListAsync();
        return rows.Select(x=>new{x.Id,x.FullName,x.Username,x.Email,x.DeletedAt,purgeAfter=x.DeletedAt!.Value.AddDays(30),canRestore=x.DeletedAt.Value.AddDays(30)>DateTime.UtcNow}).ToArray();
    }
    public async Task<object> RestoreAccount(Guid actor,Guid id,string reason)
    {
        RequireEnabled();if(string.IsNullOrWhiteSpace(reason)||reason.Length>500)throw new OnboardingException(400,"Record a restoration reason up to 500 characters.");
        await using var tx=db.Database.IsNpgsql()?await db.Database.BeginTransactionAsync():null;
        if(db.Database.IsNpgsql())await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({id.ToString()},0))");
        var user=await db.Users.Include(x=>x.Roles).SingleOrDefaultAsync(x=>x.Id==id&&x.DeletedAt!=null)??throw new OnboardingException(404,"This account is not in the recycle bin.");
        if(user.DeletedAt!.Value.AddDays(30)<=DateTime.UtcNow)throw new OnboardingException(409,"The thirty-day restoration period has ended.");
        var deletion=await db.Operations.SingleAsync(x=>x.UserId==id&&x.Kind=="AccountDeletion"&&x.State=="Recycled");
        if(lifecycle==null)throw new OnboardingException(503,"Account restoration is unavailable.");
        await lifecycle.Change("OnboardingRental",id,"restore",deletion.Id);
        user.DeletedAt=null;user.IsActive=true;user.AccessVersion++;deletion.State="Restored";deletion.Version=Guid.NewGuid();
        Audit(id,actor,"AccountRestored",reason);QueueEmail(new(Guid.NewGuid(),id,user.Email,"Restoration","Your FleetFlow account has been restored","An administrator restored your account. Sign in again; previous sessions remain revoked.",Frontend("#/login")));
        await db.SaveChangesAsync();if(tx!=null)await tx.CommitAsync();return new{success=true};
    }
    public async Task SweepLifecycle(CancellationToken ct)
    {
        RequireEnabled();
        foreach(var id in await db.Users.AsNoTracking().Where(x=>x.DeletedAt!=null&&x.DeletedAt<=DateTime.UtcNow.AddDays(-30)&&db.Operations.Any(o=>o.UserId==x.Id&&o.Kind=="AccountDeletion"&&o.State=="Recycled")).Select(x=>x.Id).Take(20).ToListAsync(ct))
        {
            db.ChangeTracker.Clear();await using var tx=db.Database.IsNpgsql()?await db.Database.BeginTransactionAsync(ct):null;
            if(db.Database.IsNpgsql())await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({id.ToString()},0))",ct);
            var user=await db.Users.Include(x=>x.Roles).SingleAsync(x=>x.Id==id,ct);if(user.DeletedAt==null||user.DeletedAt> DateTime.UtcNow.AddDays(-30))continue;
            var deletion=await db.Operations.SingleOrDefaultAsync(x=>x.UserId==id&&x.Kind=="AccountDeletion"&&x.State=="Recycled",ct);if(deletion==null)continue;
            if(lifecycle==null)throw new OnboardingException(503,"Account cleanup is unavailable.");
            await lifecycle.Change("OnboardingRental",id,"anonymize",deletion.Id,ct);
            await lifecycle.Change("OnboardingNotification",id,"anonymize",deletion.Id,ct);
            var operations=await db.Operations.Where(x=>x.UserId==id).ToListAsync(ct);
            foreach(var op in operations)
            {
                if(op.Kind=="Photo")DeletePrivatePhoto(op.Id);
                if(op.Kind=="Driver"&&op.Ciphertext!="")
                {
                    var details=vault.Decrypt<DriverDetails>(op.Ciphertext,"Driver");
                    foreach(var photo in details.Photos)DeletePrivatePhoto(photo.Id);
                }
                if(op.Kind=="Audit"&&op.Ciphertext!="")
                {
                    var audit=vault.Decrypt<System.Text.Json.JsonElement>(op.Ciphertext,"audit");
                    Guid? actor=audit.TryGetProperty("actor",out var value)&&value.ValueKind==System.Text.Json.JsonValueKind.String&&value.TryGetGuid(out var parsed)?parsed:null;
                    op.Ciphertext=vault.Encrypt(new{actor,reason="Personal audit details removed after account retention; actor, action and timestamp retained."},"audit");
                }
                else op.Ciphertext="";
                op.CodeHash="";op.AddressHash="";op.Version=Guid.NewGuid();if(op.Kind!="Audit")op.State="Anonymized";
            }
            db.DocumentClaims.RemoveRange(await db.DocumentClaims.Where(x=>x.UserId==id).ToListAsync(ct));
            db.ExternalAccounts.RemoveRange(await db.ExternalAccounts.Where(x=>x.UserId==id).ToListAsync(ct));
            if(user.ProfileImageUrl is {} profile&&profile.StartsWith("/uploads/profiles/",StringComparison.Ordinal)&&!await db.Users.AnyAsync(x=>x.Id!=id&&x.ProfileImageUrl==profile,ct))
            {
                var file=Path.GetFileName(profile);
                if(System.Text.RegularExpressions.Regex.IsMatch(file,@"^[a-fA-F0-9]{32}\.(?:jpg|jpeg|png|webp)$"))
                {
                    var uploads=configuration["IDENTITY_UPLOAD_ROOT"]??Path.Combine(AppContext.BaseDirectory,"wwwroot","uploads");
                    File.Delete(Path.Combine(Path.GetFullPath(uploads),"profiles",file));
                }
            }
            var tombstone="removed_"+id.ToString("N")+"_"+Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4));
            user.FullName="Removed customer";user.Username=tombstone;user.Email=tombstone+"@invalid.example";
            user.PhoneNumber="";user.Address="";user.DrivingLicenseNumber="";user.PasswordHash="";user.HasLocalPassword=false;user.ProfileImageUrl=null;user.EmailSecondStepEnabled=false;user.EmailVerificationMethod="Anonymized";user.AccessVersion++;
            deletion.State="Anonymized";await db.SaveChangesAsync(ct);if(tx!=null)await tx.CommitAsync(ct);
        }
        db.ChangeTracker.Clear();
        foreach(var expired in await db.Operations.Where(x=>x.Kind.StartsWith("Otp:")&&x.ExpiresAt<=DateTime.UtcNow&&x.Ciphertext!="").Take(100).ToListAsync(ct))
        {expired.Ciphertext="";expired.CodeHash="";expired.State="Expired";expired.Version=Guid.NewGuid();}
        await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();await ExpiryReminders(ct);
    }
    private void DeletePrivatePhoto(Guid photo)
    {
        var root=configuration["IDENTITY_DOCUMENT_ROOT"]??throw new InvalidOperationException("Private document storage is not configured.");
        var full=Path.GetFullPath(root);var target=Path.GetFullPath(Path.Combine(full,photo.ToString("N")+".protected"));
        if(!target.StartsWith(full.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw new InvalidOperationException("Document cleanup escaped its private root.");
        File.Delete(target);
    }
    private async Task ExpiryReminders(CancellationToken ct)
    {
        foreach(var row in await db.Operations.AsNoTracking().Where(x=>x.Kind=="Driver"&&x.State=="Approved").ToListAsync(ct))
        {
            var latest=await db.Operations.AsNoTracking().Where(x=>x.UserId==row.UserId&&x.Kind=="Driver").OrderByDescending(x=>x.CreatedAt).Select(x=>x.Id).FirstAsync(ct);
            if(latest!=row.Id)continue;
            var user=await db.Users.SingleOrDefaultAsync(x=>x.Id==row.UserId&&x.IsActive&&x.DeletedAt==null,ct);if(user==null)continue;
            var end=DriverRules.ValidUntil(vault.Decrypt<DriverDetails>(row.Ciphertext,"Driver"));if(end==null)continue;
            var days=(end.Value.AddMinutes(330).AddTicks(-1).Date-DateTime.UtcNow.AddMinutes(330).Date).Days;
            if(days is not (7 or 1))continue;
            var kind=$"ExpiryReminder:{row.Id}:{days}";
            // The sweep runs under a single database lock so every replica shares one reminder writer.
            if(await db.Operations.AnyAsync(x=>x.Kind==kind,ct))continue;
            db.Operations.Add(new(){UserId=user.Id,Kind=kind,State="Queued"});
            QueueEmail(new(Guid.NewGuid(),user.Id,user.Email,"DocumentExpiry","Your FleetFlow driving documents are nearing expiry",
                $"One or more required documents expire in approximately {days} day(s). Update your documents for administrator review before making bookings beyond their validity.",Frontend("#/customer-profile")));
        }
        await db.SaveChangesAsync(ct);
    }
}
public sealed class AccountLifecycleWorker(IServiceScopeFactory scopes,ILogger<AccountLifecycleWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while(!ct.IsCancellationRequested)
        {
            try
            {
                using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<IdentityService.Api.Data.IdentityDbContext>();
                // Session advisory lock survives the per-account transactions and serialises sweep replicas.
                await db.Database.OpenConnectionAsync(ct);
                var connection=(Npgsql.NpgsqlConnection)db.Database.GetDbConnection();await using var command=new Npgsql.NpgsqlCommand("SELECT pg_try_advisory_lock(74211003)",connection);
                if((bool)(await command.ExecuteScalarAsync(ct))!)
                {
                    try{await scope.ServiceProvider.GetRequiredService<OnboardingService>().SweepLifecycle(ct);}
                    finally{await using var unlock=new Npgsql.NpgsqlCommand("SELECT pg_advisory_unlock(74211003)",connection);await unlock.ExecuteNonQueryAsync(CancellationToken.None);}
                }
            }
            catch(Exception)when(!ct.IsCancellationRequested){logger.LogWarning("Account lifecycle processing is temporarily unavailable; it will retry.");}
            await Task.Delay(TimeSpan.FromMinutes(1),ct);
        }
    }
}
