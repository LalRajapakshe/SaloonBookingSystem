namespace SalonBooking.Application.Common;

public sealed class StoredFileContent
{
    public required Stream Content { get; init; }

    public required string ContentType { get; init; }
}
