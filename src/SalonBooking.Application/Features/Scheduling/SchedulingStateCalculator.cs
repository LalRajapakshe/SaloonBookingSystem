using SalonBooking.Domain.Enums;

namespace SalonBooking.Application.Features.Scheduling;

public static class SchedulingStateCalculator
{
    public const string Unscheduled = "Unscheduled";
    public const string PartiallyScheduled = "PartiallyScheduled";
    public const string Scheduled = "Scheduled";

    public static bool IsFullyScheduled(
        long? employeeId,
        DateTime? startTime,
        DateTime? endTime,
        bool isCancelled)
    {
        return !isCancelled &&
            employeeId is not null &&
            startTime is not null &&
            endTime is not null;
    }

    public static string Calculate(IEnumerable<ScheduleLineState> lines)
    {
        var openLines = 0;
        var scheduledLines = 0;

        foreach (var line in lines)
        {
            if (line.IsCancelled)
            {
                continue;
            }

            openLines++;
            if (line.IsFullyScheduled)
            {
                scheduledLines++;
            }
        }

        if (scheduledLines == 0)
        {
            return Unscheduled;
        }

        if (scheduledLines == openLines)
        {
            return Scheduled;
        }

        return PartiallyScheduled;
    }

    public static bool NeedsScheduling(AppointmentStatus status, string schedulingState)
    {
        var openStatus = status is AppointmentStatus.Pending
            or AppointmentStatus.Confirmed
            or AppointmentStatus.CheckedIn
            or AppointmentStatus.InProgress;

        return openStatus &&
            schedulingState is Unscheduled or PartiallyScheduled;
    }
}

public readonly record struct ScheduleLineState(bool IsCancelled, bool IsFullyScheduled);
