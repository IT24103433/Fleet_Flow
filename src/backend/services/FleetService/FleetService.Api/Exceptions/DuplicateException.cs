using System;

namespace FleetService.Api.Exceptions;

public class DuplicateException : Exception
{
    public DuplicateException(string message) : base(message)
    {
    }
}
