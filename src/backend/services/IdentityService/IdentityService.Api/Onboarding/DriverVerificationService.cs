using FleetFlow.Contracts;
using IdentityService.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Api.Onboarding;

public sealed class DriverVerificationService(IdentityDbContext db,IdentityVault vault,OnboardingService accounts,IConfiguration configuration)
{
    private const string DriverKind="Driver";
    public async Task<object> Get(Guid userId)
    {
        accounts.RequireEnabled();await accounts.Account(userId);
        var row=await Current(userId);
        if(row==null)return new{status="Not submitted",details=new DriverDetails(),revision=(Guid?)null};
        var details=vault.Decrypt<DriverDetails>(row.Ciphertext,DriverKind);
        var state=row.State=="Approved"&&DriverRules.ValidUntil(details)<=DateTime.UtcNow?"Expired":row.State;
        return new{status=state,details,revision=row.Id,commonSriLicence=details.IssuingCountry=="LK"&&DriverRules.CommonSriLicence(details.LicenceNumber)};
    }
    private Task<IdentityOperation?> Current(Guid id)=>db.Operations.Where(x=>x.UserId==id&&x.Kind==DriverKind).OrderByDescending(x=>x.CreatedAt).FirstOrDefaultAsync();
    public async Task<object> Save(Guid id,DriverDetails input,bool submit)
    {
        accounts.RequireEnabled();var user=await accounts.Account(id);
        input.LegalName=input.LegalName?.Trim()??"";input.PhoneNumber=input.PhoneNumber?.Trim()??"";input.Address=input.Address?.Trim()??"";
        input.IssuingCountry=input.IssuingCountry?.Trim().ToUpperInvariant()??"";input.IdentityNumber=input.IdentityNumber?.Trim()??"";
        input.LicenceNumber=input.LicenceNumber?.Trim()??"";input.PermitNumber=input.PermitNumber?.Trim()??"";
        if(input.LegalName.Length>100||input.PhoneNumber.Length>20||input.Address.Length>250||input.IdentityNumber.Length>50||input.LicenceNumber.Length>50||input.PermitNumber.Length>50)
            throw new OnboardingException(400,"A driving-detail field exceeds its supported length.");
        if(!user.Roles.Any(x=>x.Name=="CUSTOMER")||user.Roles.Any(x=>x.Name=="ADMIN"||x.Name=="FLEET_MANAGER"||x.Name=="MAINTENANCE_STAFF"))throw new OnboardingException(403,"Driver verification is available to customer accounts.");
        var current=await Current(id);var previous=current==null?new DriverDetails():vault.Decrypt<DriverDetails>(current.Ciphertext,DriverKind);
        // Never accept caller-provided files, aliases or review metadata as evidence.
        input.Photos=previous.Photos;input.NicAliases=previous.NicAliases;input.ReviewedBy=null;input.ReviewedAt=null;input.ReviewReason=null;
        if(input.IdentityType=="NIC")input.IdentityNumber=DriverRules.SriNumber(input.IdentityNumber);
        if(input.IssuingCountry=="LK")input.LicenceNumber=DriverRules.SriNumber(input.LicenceNumber);
        if(submit&&DriverRules.Error(input) is {} error)throw new OnboardingException(400,error);
        if(current==null||current.State is "Approved" or "Rejected")
        {current=new(){UserId=id,Kind=DriverKind};db.Operations.Add(current);}
        current.State=submit?"Pending review":"Draft";current.Ciphertext=vault.Encrypt(input,DriverKind);current.Version=Guid.NewGuid();
        if(submit)
        {
            accounts.Audit(id,id,"DriverSubmitted","Driver evidence submitted for administrator review.");
            accounts.QueueEmail(new(Guid.NewGuid(),id,user.Email,"DriverReview","Your FleetFlow documents are awaiting review","We received your documents. You can book after an administrator approves them.",ActionUrl:accounts.Frontend("#/customer-profile")));
        }
        await db.SaveChangesAsync();return await Get(id);
    }
    private string FilePath(Guid id)
    {
        var root=configuration["IDENTITY_DOCUMENT_ROOT"];
        if(string.IsNullOrWhiteSpace(root))throw new OnboardingException(503,"Private document storage is not configured.");
        var full=Path.GetFullPath(root);
        if(full.Contains("wwwroot",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Identity documents must be stored outside public web roots.");
        Directory.CreateDirectory(full);return Path.Combine(full,id.ToString("N")+".protected");
    }
    public async Task<object> Upload(Guid userId,string kind,IFormFile file)
    {
        accounts.RequireEnabled();await accounts.Account(userId);
        if(kind is not ("DL" or "NIC" or "Passport" or "IDP"))throw new OnboardingException(400,"Choose a supported document type.");
        if(file.Length is <=0 or >5*1024*1024)throw new OnboardingException(400,"Choose a document photograph up to 5 MB.");
        var current=await Current(userId);
        if(current==null||current.State!="Draft")throw new OnboardingException(409,"Save a draft before uploading or replacing documents.");
        var details=vault.Decrypt<DriverDetails>(current.Ciphertext,DriverKind);
        if(details.Photos.Count(x=>x.Kind==kind)>=2||details.Photos.Sum(x=>x.Bytes)+file.Length>25L*1024*1024)throw new OnboardingException(400,"The document or application upload limit was reached.");
        await using var buffer=new MemoryStream();await file.CopyToAsync(buffer);var bytes=buffer.ToArray();
        var type=ImageType(bytes);if(type==null||!string.Equals(type,file.ContentType,StringComparison.OrdinalIgnoreCase))throw new OnboardingException(400,"Upload a JPEG, PNG or WebP photograph with its matching content type.");
        bytes=DocumentImage.Sanitize(bytes);type="image/jpeg";
        if(details.Photos.Sum(x=>x.Bytes)+bytes.LongLength>25L*1024*1024)throw new OnboardingException(400,"The safely processed application exceeds 25 MB.");
        var photo=new DriverPhoto(Guid.NewGuid(),kind,type,bytes.LongLength);var path=FilePath(photo.Id);
        await File.WriteAllTextAsync(path,vault.Encrypt(bytes,"document:"+photo.Id));
        details.Photos.Add(photo);current.Ciphertext=vault.Encrypt(details,DriverKind);current.Version=Guid.NewGuid();
        db.Operations.Add(new(){Id=photo.Id,UserId=userId,Kind="Photo",State="Stored",Ciphertext=vault.Encrypt(photo,"photo")});
        try{await db.SaveChangesAsync();}catch{File.Delete(path);throw;}
        return await Get(userId);
    }
    public static string? ImageType(byte[] b)
    {
        if(b.Length<16)return null;
        if(b[0]==0xff&&b[1]==0xd8&&b[2]==0xff&&b[^2]==0xff&&b[^1]==0xd9)return "image/jpeg";
        if(b.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})&&b.AsSpan(12,4).SequenceEqual("IHDR"u8))return "image/png";
        if(b.AsSpan(0,4).SequenceEqual("RIFF"u8)&&b.AsSpan(8,4).SequenceEqual("WEBP"u8))return "image/webp";
        return null;
    }
    public async Task<(byte[] Bytes,string Type)> Photo(Guid owner,Guid photoId,bool admin)
    {
        accounts.RequireEnabled();await accounts.Account(owner);
        var row=await db.Operations.SingleOrDefaultAsync(x=>x.Id==photoId&&x.Kind=="Photo"&&(admin||x.UserId==owner));
        if(row==null||row.State!="Stored"||row.Ciphertext=="")throw new OnboardingException(404,"Document photograph not found.");
        var photo=vault.Decrypt<DriverPhoto>(row.Ciphertext,"photo");
        return(vault.Decrypt<byte[]>(await File.ReadAllTextAsync(FilePath(photo.Id)),"document:"+photo.Id),photo.ContentType);
    }
    public async Task<object> RemovePhoto(Guid id,Guid photoId)
    {
        accounts.RequireEnabled();await accounts.Account(id);var current=await Current(id);
        if(current==null||current.State!="Draft")throw new OnboardingException(409,"Only draft documents can be removed.");
        var details=vault.Decrypt<DriverDetails>(current.Ciphertext,DriverKind);if(details.Photos.RemoveAll(x=>x.Id==photoId)==0)throw new OnboardingException(404,"Photograph not found.");
        current.Ciphertext=vault.Encrypt(details,DriverKind);current.Version=Guid.NewGuid();await db.SaveChangesAsync();
        // A prior reviewed revision may still reference the encrypted file; cleanup handles it later.
        return await Get(id);
    }
    public async Task<object> Queue()
    {
        accounts.RequireEnabled();var rows=await db.Operations.Where(x=>x.Kind==DriverKind&&x.State=="Pending review").OrderBy(x=>x.CreatedAt).ToListAsync();
        var result=new List<object>();
        foreach(var row in rows)
        {
            var details=vault.Decrypt<DriverDetails>(row.Ciphertext,DriverKind);var claims=Claims(details).ToArray();
            var duplicate=false;foreach(var claim in claims)duplicate|=await db.DocumentClaims.AnyAsync(x=>x.UserId!=row.UserId&&x.Kind==claim.Kind&&x.Fingerprint==claim.Hash);
            result.Add(new{row.Id,row.UserId,row.CreatedAt,details,duplicate,commonSriLicence=details.IssuingCountry=="LK"&&DriverRules.CommonSriLicence(details.LicenceNumber)});
        }
        return result;
    }
    private IEnumerable<(string Kind,string Hash)> Claims(DriverDetails details)
    {
        if(details.IssuingCountry!="LK")yield break;
        yield return("LK:DL",vault.Fingerprint("LK:DL",DriverRules.SriNumber(details.LicenceNumber)));
        if(details.IdentityType=="NIC")foreach(var nic in details.NicAliases.Append(details.IdentityNumber).Select(DriverRules.SriNumber).Distinct())yield return("LK:NIC",vault.Fingerprint("LK:NIC",nic));
    }
    public async Task<object> Review(Guid actor,Guid revision,ReviewInput request)
    {
        accounts.RequireEnabled();await using var tx=db.Database.IsNpgsql()?await db.Database.BeginTransactionAsync():null;
        if((request.Reason?.Length??0)>500||(request.NicAliases?.Count??0)>5)throw new OnboardingException(400,"Use a review note up to 500 characters and at most five evidenced NIC aliases.");
        if(db.Database.IsNpgsql())await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({revision.ToString()},0))");
        var row=await db.Operations.SingleOrDefaultAsync(x=>x.Id==revision&&x.Kind==DriverKind);
        if(row==null||row.State!="Pending review")throw new OnboardingException(409,"This application is no longer awaiting review.");
        var latest=await Current(row.UserId!.Value);if(latest?.Id!=row.Id)throw new OnboardingException(409,"The customer has a newer document revision.");
        var customer=await accounts.Account(row.UserId.Value);var details=vault.Decrypt<DriverDetails>(row.Ciphertext,DriverKind);
        if(!request.Approve&&string.IsNullOrWhiteSpace(request.Reason))throw new OnboardingException(400,"Give the customer a reason and correction instructions.");
        if(request.Approve)
        {
            if(DriverRules.Error(details) is {} error)throw new OnboardingException(400,error);
            foreach(var alias in request.NicAliases??[])
            {if(details.IssuingCountry!="LK"||details.IdentityType!="NIC"||!DriverRules.ValidNic(alias))throw new OnboardingException(400,"Confirm only valid NIC aliases supported by the evidence.");details.NicAliases.Add(DriverRules.SriNumber(alias));}
            foreach(var claim in Claims(details))
            {
                var existing=await db.DocumentClaims.SingleOrDefaultAsync(x=>x.Kind==claim.Kind&&x.Fingerprint==claim.Hash);
                if(existing!=null&&existing.UserId!=customer.Id)throw new OnboardingException(409,"A document identifier already belongs to another approved account. Approval cannot be forced.");
                if(existing==null)db.DocumentClaims.Add(new(){UserId=customer.Id,Kind=claim.Kind,Fingerprint=claim.Hash,RevisionId=row.Id});
            }
        }
        details.ReviewedBy=actor;details.ReviewedAt=DateTime.UtcNow;details.ReviewReason=request.Reason?.Trim()??"";row.State=request.Approve?"Approved":"Rejected";row.Version=Guid.NewGuid();row.Ciphertext=vault.Encrypt(details,DriverKind);
        accounts.Audit(customer.Id,actor,"Driver"+row.State,details.ReviewReason);
        accounts.QueueEmail(new(Guid.NewGuid(),customer.Id,customer.Email,"DriverReview",request.Approve?"Your FleetFlow driver verification is approved":"Please update your FleetFlow driver documents",
            request.Approve?"Your documents have been approved. Each booking must remain within their validity period.":details.ReviewReason,ActionUrl:accounts.Frontend("#/customer-profile")));
        await db.SaveChangesAsync();if(tx!=null)await tx.CommitAsync();return new{status=row.State};
    }
    public async Task<DriverEligibility> Eligibility(Guid id,DateTime end)
    {
        if(!accounts.Enabled)return new(false,false,"Disabled","The verified booking policy is not activated.",null,null);
        var user=await accounts.Account(id);var current=await Current(id);
        if(current==null||current.State!="Approved")return new(true,false,current?.State??"Not submitted","Complete driver verification and administrator review before booking.",current?.Id,null);
        var details=vault.Decrypt<DriverDetails>(current.Ciphertext,DriverKind);var expiry=DriverRules.ValidUntil(details);
        if(expiry==null||end.ToUniversalTime()>=expiry)return new(true,false,"Expired","Your documents must remain valid through the return date.",current.Id,expiry);
        if(user.EmailVerificationMethod=="Unverified")return new(true,false,"Email required","Verify your email before making a new booking.",current.Id,expiry);
        return new(true,true,"Approved",null,current.Id,expiry);
    }
}
