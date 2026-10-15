using System.Text.RegularExpressions;

namespace IdentityService.Api.Onboarding;

public static partial class DriverRules
{
    public static string SriNumber(string value)=>Regex.Replace(value.Trim().ToUpperInvariant(),@"[\s-]","");
    public static bool ValidNic(string value)=>NicPattern().IsMatch(SriNumber(value));
    public static bool CommonSriLicence(string value)=>SriLicencePattern().IsMatch(SriNumber(value));
    public static DateTime? ValidUntil(DriverDetails details)
    {
        var dates=new List<DateOnly>();if(details.LicenceExpiry is {} licence)dates.Add(licence);
        if(details.IdentityType=="Passport"&&details.PassportExpiry is {} passport)dates.Add(passport);
        if(details.IssuingCountry!="LK"&&details.PermitExpiry is {} permit)dates.Add(permit);
        // Expiry is a date in the application's Sri Lankan rental timezone.
        return dates.Count==0?null:dates.Min()==DateOnly.MaxValue?DateTime.SpecifyKind(DateTime.MaxValue,DateTimeKind.Utc):DateTime.SpecifyKind(dates.Min().AddDays(1).ToDateTime(TimeOnly.MinValue).AddMinutes(-330),DateTimeKind.Utc);
    }
    public static string? Error(DriverDetails d,bool photos=true)
    {
        if(d.LegalName.Trim().Length is <2 or >100)return "Enter your full legal name exactly as it appears on your documents.";
        if(!PhonePattern().IsMatch(d.PhoneNumber))return "Enter a valid international phone number with its country code.";
        if(d.Address.Trim().Length is <5 or >250)return "Enter your current address.";
        if(!Regex.IsMatch(d.IssuingCountry,@"^[A-Z]{2}$"))return "Choose the licence-issuing country.";
        if(d.LicenceNumber.Trim().Length is <4 or >50||!Regex.IsMatch(d.LicenceNumber,@"^[\p{L}\p{N} /.-]+$"))return "Enter the licence number shown on your document.";
        if(d.LicenceExpiry==null)return "Enter the driving licence expiry date.";
        if(d.LicenceExpiry?.Year>2100||d.PassportExpiry?.Year>2100||d.PermitExpiry?.Year>2100)return "Enter a realistic document expiry date.";
        if(d.IdentityType is not ("NIC" or "Passport"))return "Choose NIC or passport identification.";
        if(d.IdentityType=="NIC"&&(d.IssuingCountry!="LK"||!ValidNic(d.IdentityNumber)))return "Enter a Sri Lankan NIC with 9 digits and V/X, or 12 digits.";
        if(d.IdentityType=="Passport"&&(d.IdentityNumber.Trim().Length is <4 or >50||d.PassportExpiry==null))return "Enter your passport number and expiry date.";
        if(d.IssuingCountry!="LK"&&(d.IdentityType!="Passport"||d.PermitNumber.Trim().Length is <4 or >50||d.PermitExpiry==null))return "Foreign licences require a passport and International Driving Permit with their expiry dates.";
        if(ValidUntil(d)<=DateTime.UtcNow)return "Expired driving or identification documents cannot be approved.";
        if(d.Photos.Count>6||d.Photos.Sum(p=>p.Bytes)>25L*1024*1024)return "An application can contain at most 25 MB of document photographs.";
        if(d.Photos.Any(p=>p.Kind is not ("DL" or "NIC" or "Passport" or "IDP"))||d.Photos.GroupBy(p=>p.Kind).Any(g=>g.Count()>2))return "Upload at most two photographs per document.";
        if(photos&&(!d.Photos.Any(p=>p.Kind=="DL")||!d.Photos.Any(p=>p.Kind==d.IdentityType)||(d.IssuingCountry!="LK"&&!d.Photos.Any(p=>p.Kind=="IDP"))))return "Upload the required licence, identification and permit photographs.";
        return null;
    }
    [GeneratedRegex(@"^(?:[0-9]{9}[VX]|[0-9]{12})$")] private static partial Regex NicPattern();
    [GeneratedRegex(@"^B[0-9]{7}$")] private static partial Regex SriLicencePattern();
    [GeneratedRegex(@"^\+[1-9][0-9]{6,14}$")] private static partial Regex PhonePattern();
}
