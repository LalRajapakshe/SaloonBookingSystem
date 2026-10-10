namespace SalonBooking.Application.Features.Scheduling;

public readonly record struct TimeRange(DateTime Start, DateTime End);

public static class AvailabilityMath
{
    public const int SlotStepMinutes = 15;

    public static IReadOnlyList<TimeRange> Subtract(
        IReadOnlyList<TimeRange> windows,
        IReadOnlyList<TimeRange> blocks)
    {
        var current = windows
            .Where(window => window.End > window.Start)
            .ToList();

        foreach (var block in blocks.Where(block => block.End > block.Start))
        {
            current = SubtractOne(current, block);
        }

        return current;
    }

    public static IReadOnlyList<TimeRange> Slots(
        IReadOnlyList<TimeRange> freeWindows,
        int durationMinutes)
    {
        if (durationMinutes <= 0)
        {
            return [];
        }

        var step = TimeSpan.FromMinutes(SlotStepMinutes);
        var length = TimeSpan.FromMinutes(durationMinutes);
        var slots = new List<TimeRange>();

        foreach (var window in freeWindows)
        {
            for (var start = window.Start; start + length <= window.End; start += step)
            {
                slots.Add(new TimeRange(start, start + length));
            }
        }

        return slots;
    }

    public static bool Fits(IReadOnlyList<TimeRange> freeWindows, DateTime start, DateTime end)
    {
        return freeWindows.Any(window => window.Start <= start && window.End >= end);
    }

    private static List<TimeRange> SubtractOne(
        List<TimeRange> windows,
        TimeRange block)
    {
        var result = new List<TimeRange>();

        foreach (var window in windows)
        {
            if (block.End <= window.Start || block.Start >= window.End)
            {
                result.Add(window);
                continue;
            }

            if (block.Start > window.Start)
            {
                result.Add(new TimeRange(window.Start, block.Start));
            }

            if (block.End < window.End)
            {
                result.Add(new TimeRange(block.End, window.End));
            }
        }

        return result;
    }
}
