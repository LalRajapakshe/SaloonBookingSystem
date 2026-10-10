using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Features.Scheduling;
using SalonBooking.Domain.Enums;
using SalonBooking.Persistence.Context;

namespace SalonBooking.Infrastructure.Scheduling;

internal static class EmployeeAvailabilityLoader
{
    public static async Task<IReadOnlyList<TimeRange>> GetFreeWindowsAsync(
        SalonBookingDbContext context,
        long branchId,
        long employeeId,
        DateOnly date,
        long? excludedAppointmentServiceId,
        CancellationToken cancellationToken)
    {
        var dayStart = date.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);
        var dayOfWeek = (byte)date.DayOfWeek;

        var segments = await context.StaffSchedules
            .Where(segment =>
                segment.EmployeeId == employeeId &&
                segment.BranchId == branchId &&
                segment.DayOfWeek == dayOfWeek &&
                segment.IsActive)
            .ToListAsync(cancellationToken);

        var working = segments
            .Where(segment => segment.SegmentType == ScheduleSegmentType.Working)
            .Select(segment => new TimeRange(
                date.ToDateTime(segment.StartTime),
                date.ToDateTime(segment.EndTime)))
            .ToList();

        var blocks = segments
            .Where(segment => segment.SegmentType == ScheduleSegmentType.Break)
            .Select(segment => new TimeRange(
                date.ToDateTime(segment.StartTime),
                date.ToDateTime(segment.EndTime)))
            .ToList();

        var leave = await context.StaffLeaves
            .Where(item =>
                item.EmployeeId == employeeId &&
                item.BranchId == branchId &&
                item.Status == LeaveStatus.Approved &&
                item.StartDateTime < dayEnd &&
                item.EndDateTime > dayStart)
            .Select(item => new { item.StartDateTime, item.EndDateTime })
            .ToListAsync(cancellationToken);

        blocks.AddRange(leave.Select(item => new TimeRange(
            item.StartDateTime < dayStart ? dayStart : item.StartDateTime,
            item.EndDateTime > dayEnd ? dayEnd : item.EndDateTime)));

        var bookings = await context.AppointmentServices
            .Where(line =>
                line.EmployeeId == employeeId &&
                line.BranchId == branchId &&
                !line.IsCancelled &&
                line.StartTime != null &&
                line.EndTime != null &&
                line.StartTime < dayEnd &&
                line.EndTime > dayStart &&
                line.AppointmentServiceId != excludedAppointmentServiceId)
            .Select(line => new { line.StartTime, line.EndTime })
            .ToListAsync(cancellationToken);

        blocks.AddRange(bookings.Select(line => new TimeRange(
            line.StartTime!.Value,
            line.EndTime!.Value)));

        return AvailabilityMath.Subtract(working, blocks);
    }
}
