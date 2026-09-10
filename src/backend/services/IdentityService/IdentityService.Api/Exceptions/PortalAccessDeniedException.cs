namespace IdentityService.Api.Exceptions;

public class PortalAccessDeniedException : Exception
{
    public PortalAccessDeniedException()
        : base("Your account does not have access to this portal.") { }

    public PortalAccessDeniedException(string message)
        : base(message) { }
}
