using System.ComponentModel.DataAnnotations;

namespace IdentityService.Api.Onboarding;

public sealed class IdentityOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public string Kind { get; set; } = "";
    public string State { get; set; } = "Draft";
    public string Ciphertext { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public string AddressHash { get; set; } = "";
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
public sealed class DocumentClaim
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Kind { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public Guid RevisionId { get; set; }
}
public sealed class ExternalAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Provider { get; set; } = "Google";
    public string Subject { get; set; } = "";
}
public sealed class SignupDetails
{
    [Required, StringLength(100, MinimumLength=2)] public string FullName { get; set; } = "";
    [Required, StringLength(50, MinimumLength=3), RegularExpression("^[a-zA-Z0-9_-]+$")] public string Username { get; set; } = "";
    [Required, EmailAddress, StringLength(100)] public string Email { get; set; } = "";
    [Required, StringLength(100, MinimumLength=8), RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).+$")]
    public string Password { get; set; } = "";
    [Required, Compare(nameof(Password))] public string ConfirmPassword { get; set; } = "";
}
public sealed record ChallengeInput(Guid ChallengeId, string Code, bool UseRecoveryCode = false);
public sealed record GoogleInput(Guid ChallengeId, string Credential, string? Username, string? FullName, string? Password=null, string? RecoveryCode=null);
public sealed record GoogleSignupInput(Guid SignupId, string Username, string FullName);
public sealed class ProfileDetails
{
    [Required, StringLength(100, MinimumLength=2)] public string FullName { get; set; } = "";
    [Required, StringLength(50, MinimumLength=3), RegularExpression("^[a-zA-Z0-9_-]+$")] public string Username { get; set; } = "";
    [StringLength(20)] public string PhoneNumber { get; set; } = "";
    [StringLength(250)] public string Address { get; set; } = "";
}
public sealed class DriverDetails
{
    public string LegalName { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Address { get; set; } = "";
    public string IssuingCountry { get; set; } = "LK";
    public string IdentityType { get; set; } = "NIC";
    public string IdentityNumber { get; set; } = "";
    public string LicenceNumber { get; set; } = "";
    public DateOnly? LicenceExpiry { get; set; }
    public DateOnly? PassportExpiry { get; set; }
    public string PermitNumber { get; set; } = "";
    public DateOnly? PermitExpiry { get; set; }
    public List<string> NicAliases { get; set; } = [];
    public List<DriverPhoto> Photos { get; set; } = [];
    public string? ReviewReason { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
public sealed record DriverPhoto(Guid Id, string Kind, string ContentType, long Bytes);
public sealed record ReviewInput(bool Approve, string Reason, List<string>? NicAliases = null);
public sealed record SecurityInput(string Password, string? Code = null, string? NewEmail = null, Guid? ReauthenticationId = null);
public sealed record RecoverInput(string Email);
public sealed class ResetInput
{
    public Guid ChallengeId { get; set; }
    [Required] public string Code { get; set; } = "";
    [Required, StringLength(100, MinimumLength=8), RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).+$")]
    public string NewPassword { get; set; } = "";
}
public sealed class OnboardingException(int status, string message):Exception(message)
{ public int Status { get; } = status; }
