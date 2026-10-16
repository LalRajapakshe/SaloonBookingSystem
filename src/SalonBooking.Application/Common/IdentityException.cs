namespace SalonBooking.Application.Common;

public class IdentityException : Exception
{
    public IdentityException(string message)
        : base(message)
    {
    }
}
