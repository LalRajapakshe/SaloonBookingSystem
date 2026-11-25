namespace SalonBooking.Application.Common;

public sealed class FeedbackRequiredException : Exception
{
    public FeedbackRequiredException()
        : base("Feedback is required before continuing.")
    {
    }
}
