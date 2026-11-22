namespace SalonBooking.Application.Features.Scheduling;

/// Asia/Colombo has no daylight saving. Stored appointment times stay as
/// salon wall-clock values and are not converted to UTC.
public static class SalonTime
{
    public static readonly TimeSpan Offset = new(5, 30, 0);

    public static DateTime Now(DateTime? utcNow = null)
    {
        var utc = utcNow ?? DateTime.UtcNow;
        if (utc.Kind == DateTimeKind.Local)
        {
            utc = utc.ToUniversalTime();
        }

        utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return DateTime.SpecifyKind(utc.Add(Offset), DateTimeKind.Unspecified);
    }

    public static DateOnly Today(DateTime? utcNow = null) => DateOnly.FromDateTime(Now(utcNow));

    /// A slot is past only on the salon's current calendar day.
    /// Earlier dates keep their stored schedule so historical reads stay stable.
    public static bool IsPastSlot(DateOnly date, DateTime slotStart, DateTime? utcNow = null)
    {
        var now = Now(utcNow);
        if (date != DateOnly.FromDateTime(now))
        {
            return false;
        }

        var start = DateTime.SpecifyKind(slotStart, DateTimeKind.Unspecified);
        return start <= now;
    }
}
