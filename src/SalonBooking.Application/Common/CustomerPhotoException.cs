namespace SalonBooking.Application.Common;

public sealed class CustomerPhotoException : Exception
{
    public CustomerPhotoException(string message)
        : base(message)
    {
    }
}
