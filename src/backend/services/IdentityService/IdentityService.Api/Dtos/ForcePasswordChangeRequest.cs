namespace IdentityService.Api.Dtos;

public class ForcePasswordChangeRequest
{
    public bool MustChangePassword { get; set; } = true;
}
