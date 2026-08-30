using System;

namespace IdentityService.Api.Exceptions;

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException() : base("Invalid username/email or password.")
    {
    }

    public InvalidCredentialsException(string message) : base(message)
    {
    }
}
