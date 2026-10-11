namespace SalonBooking.Application.Common;

public sealed class CustomerPhotoContent
{
    public required Stream Content { get; init; }

    public required string ContentType { get; init; }
}
