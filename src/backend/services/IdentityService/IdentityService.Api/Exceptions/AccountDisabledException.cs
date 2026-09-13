using System;

namespace IdentityService.Api.Exceptions;

public class AccountDisabledException : Exception
{
    public AccountDisabledException() : base("Your account has been disabled. Please contact an administrator.")
    {
    }

    public AccountDisabledException(string message) : base(message)
    {
    }
}
