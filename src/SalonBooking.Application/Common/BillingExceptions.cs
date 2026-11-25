namespace SalonBooking.Application.Common;

public class BillingException : Exception
{
    public BillingException(string message)
        : base(message)
    {
    }
}

public sealed class BillingConflictException : BillingException
{
    public BillingConflictException(string message)
        : base(message)
    {
    }
}
