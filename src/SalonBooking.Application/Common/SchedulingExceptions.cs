namespace SalonBooking.Application.Common;

public class SchedulingException : Exception
{
    public SchedulingException(string message)
        : base(message)
    {
    }
}

public sealed class SchedulingConflictException : SchedulingException
{
    public SchedulingConflictException(string message)
        : base(message)
    {
    }
}
