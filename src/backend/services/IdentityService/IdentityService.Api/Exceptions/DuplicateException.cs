namespace IdentityService.Api.Exceptions;

public class DuplicateException : Exception
{
    public DuplicateException(string message) : base(message)
    {
    }
}
