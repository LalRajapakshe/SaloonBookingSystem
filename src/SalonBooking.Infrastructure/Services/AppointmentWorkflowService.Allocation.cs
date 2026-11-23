using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Scheduling;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Infrastructure.Scheduling;

namespace SalonBooking.Infrastructure.Services;

public sealed partial class AppointmentWorkflowService
{
    public async Task<AllocationRecommendationResponse> RecommendAllocationAsync(
        long appointmentId,
        CancellationToken cancellationToken = default)
    {
        var appointment = await LoadAllocationAppointmentAsync(appointmentId, cancellationToken);
        var openLines = OpenUnscheduledLines(appointment);
        var requestedTime = openLines.Select(line => line.PreferredStartTime).FirstOrDefault(time => time != null)
            ?? appointment.AppointmentServices.Where(line => !line.IsDeleted).Select(line => line.PreferredStartTime).FirstOrDefault(time => time != null);
        var preferredId = openLines.Select(line => line.PreferredEmployeeId).FirstOrDefault(id => id is > 0);
        var names = await StylistNamesAsync(
            openLines.Select(line => line.PreferredEmployeeId).Where(id => id is > 0).Select(id => id!.Value),
            cancellationToken);
        var response = new AllocationRecommendationResponse
        {
            AppointmentId = appointment.AppointmentId,
            RequestedDate = appointment.AppointmentDate,
            RequestedTime = requestedTime,
            PreferredStylistName = preferredId is > 0 && names.TryGetValue(preferredId.Value, out var preferred)
                ? preferred
                : null,
            ServiceNames = openLines
                .Select(line => line.Service?.ServiceName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToList()
        };
        if (openLines.Count == 0) return response;

        var eligible = new Dictionary<long, List<long>>();
        foreach (var serviceId in openLines.Select(line => line.ServiceId).Distinct())
        {
            eligible[serviceId] = await StaffStylistIdsAsync(
                appointment.TenantId, appointment.BranchId, serviceId, cancellationToken);
        }

        var stylistIds = eligible.Values.SelectMany(ids => ids).Distinct().ToList();
        var stylistNames = await StylistNamesAsync(stylistIds, cancellationToken);
        var choices = new List<AllocationChoice>();
        var dates = CandidateDates(appointment.AppointmentDate);
        foreach (var date in dates)
        {
            var dayChoices = await ChoicesForDateAsync(
                appointment, openLines, eligible, stylistNames, date, requestedTime, cancellationToken);
            choices.AddRange(dayChoices);
            if (date == appointment.AppointmentDate && dayChoices.Count > 0)
            {
                break;
            }
        }

        response.Options = AllocationRanker.Rank(choices).ToList();
        return response;
    }

    public async Task<AppointmentResponse> ApplyAllocationAsync(
        long appointmentId,
        ApplyAllocationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Lines.Count == 0)
        {
            throw new SchedulingException("Choose an allocation before saving.");
        }

        await RequireAppointmentAsync(appointmentId, cancellationToken);
        await using var transaction = await _context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);
        await LockAppointmentRowAsync(appointmentId, cancellationToken);
        _context.ChangeTracker.Clear();
        var appointment = await _context.Appointments
            .Include(item => item.AppointmentServices)
            .FirstAsync(item => item.AppointmentId == appointmentId && !item.IsDeleted, cancellationToken);
        EnsureBranchAccess(appointment);
        if (!IsOpenBooking(appointment.Status))
        {
            throw new SchedulingException("This appointment can no longer be scheduled.");
        }

        var targetDate = DateOnly.FromDateTime(request.Lines[0].StartTime);
        if (request.Lines.Any(line => DateOnly.FromDateTime(line.StartTime) != targetDate ||
                                      DateOnly.FromDateTime(line.EndTime) != targetDate))
        {
            throw new SchedulingException("All services in one allocation must be on the same date.");
        }

        if (targetDate != appointment.AppointmentDate)
        {
            if (appointment.Status != AppointmentStatus.Pending ||
                appointment.AppointmentServices.Any(line => !line.IsDeleted && !line.IsCancelled && line.StartTime != null))
            {
                throw new SchedulingException("The appointment date can change only before a slot is saved.");
            }

            appointment.AppointmentDate = targetDate;
        }

        var placed = new List<(long EmployeeId, DateTime Start, DateTime End)>();
        foreach (var item in request.Lines.OrderBy(line => line.StartTime))
        {
            var line = appointment.AppointmentServices.FirstOrDefault(candidate =>
                candidate.AppointmentServiceId == item.AppointmentServiceId && !candidate.IsDeleted);
            if (line == null) throw new SchedulingException("A service on this allocation could not be found.");
            if (line.IsCancelled || line.LineStatus == ServiceLineStatus.Cancelled)
            {
                throw new SchedulingException("A cancelled service line cannot be scheduled.");
            }

            if (line.LineStatus == ServiceLineStatus.Completed)
            {
                throw new SchedulingException("A completed service cannot be reassigned.");
            }
            ApplyRowVersion(line, item.RowVersion);
            if (item.EndTime <= item.StartTime)
            {
                throw new SchedulingException("End time must be after start time.");
            }

            if ((int)(item.EndTime - item.StartTime).TotalMinutes != line.DurationMinutes)
            {
                throw new SchedulingException("The scheduled length must match the service duration.");
            }

            if (Overlaps(item.StartTime, item.EndTime, placed) ||
                appointment.AppointmentServices.Any(other =>
                    other.AppointmentServiceId != line.AppointmentServiceId &&
                    !other.IsCancelled &&
                    other.StartTime != null &&
                    other.EndTime != null &&
                    other.StartTime < item.EndTime &&
                    other.EndTime > item.StartTime))
            {
                throw new SchedulingConflictException("The services in this allocation overlap.");
            }

            var capable = await _context.EmployeeServices.AnyAsync(
                capability =>
                    capability.EmployeeId == item.EmployeeId &&
                    capability.ServiceId == line.ServiceId &&
                    capability.BranchId == appointment.BranchId &&
                    capability.IsActive &&
                    !capability.IsDeleted,
                cancellationToken);
            if (!capable)
            {
                throw new SchedulingException("The employee is not able to perform this service.");
            }

            var free = await EmployeeAvailabilityLoader.GetFreeWindowsAsync(
                _context,
                appointment.TenantId,
                appointment.BranchId,
                item.EmployeeId,
                targetDate,
                line.AppointmentServiceId,
                cancellationToken);
            var sameStylist = placed
                .Where(slot => slot.EmployeeId == item.EmployeeId)
                .Select(slot => new TimeRange(slot.Start, slot.End))
                .ToList();
            free = AvailabilityMath.Subtract(free, sameStylist);
            if (!AvailabilityMath.Fits(free, item.StartTime, item.EndTime))
            {
                throw new SchedulingConflictException("The selected slot is no longer available.");
            }

            var previousEmployeeId = line.EmployeeId;
            var previousStatus = line.LineStatus;
            line.EmployeeId = item.EmployeeId;
            line.StartTime = item.StartTime;
            line.EndTime = item.EndTime;
            RecordAssignment(line, previousEmployeeId, previousStatus, null);
            TouchRowVersion(line);
            placed.Add((item.EmployeeId, item.StartTime, item.EndTime));
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new SchedulingConflictException("The selected slot is no longer available.");
        }

        await transaction.CommitAsync(cancellationToken);
        return await MapLoadedAppointmentAsync(appointment, cancellationToken);
    }

    private void EnsureBranchAccess(Appointment appointment)
    {
        if (_currentUser.BranchId is > 0 && _currentUser.BranchId != appointment.BranchId)
        {
            throw new SchedulingException("That appointment is outside this branch.");
        }

        if (_currentUser.TenantId is > 0 && _currentUser.TenantId != appointment.TenantId)
        {
            throw new SchedulingException("That appointment is outside this salon.");
        }
    }

    private static bool Overlaps(
        DateTime start,
        DateTime end,
        IEnumerable<(long EmployeeId, DateTime Start, DateTime End)> placed) =>
        placed.Any(slot => slot.Start < end && slot.End > start);

    private async Task<Appointment> LoadAllocationAppointmentAsync(long appointmentId, CancellationToken cancellationToken)
    {
        var appointment = await RequireAppointmentAsync(appointmentId, cancellationToken);
        return await _context.Appointments
            .AsNoTracking()
            .Include(item => item.AppointmentServices)
                .ThenInclude(line => line.Service)
            .FirstAsync(item => item.AppointmentId == appointment.AppointmentId, cancellationToken);
    }

    private static List<AppointmentService> OpenUnscheduledLines(Appointment appointment) =>
        appointment.AppointmentServices
            .Where(line => !line.IsDeleted && !line.IsCancelled)
            .Where(line => !SchedulingStateCalculator.IsFullyScheduled(line.EmployeeId, line.StartTime, line.EndTime, false))
            .OrderBy(line => line.Sequence)
            .ToList();

    private static IEnumerable<DateOnly> CandidateDates(DateOnly requested)
    {
        yield return requested;
        var today = SalonTime.Today();
        for (var distance = 1; distance <= 3; distance++)
        {
            var after = requested.AddDays(distance);
            var before = requested.AddDays(-distance);
            if (after >= today) yield return after;
            if (before >= today) yield return before;
        }
    }

    private async Task<List<AllocationChoice>> ChoicesForDateAsync(
        Appointment appointment,
        List<AppointmentService> lines,
        Dictionary<long, List<long>> eligible,
        IReadOnlyDictionary<long, string> stylistNames,
        DateOnly date,
        DateTime? requestedTime,
        CancellationToken cancellationToken)
    {
        var windows = new Dictionary<long, IReadOnlyList<TimeRange>>();
        foreach (var employeeId in eligible.Values.SelectMany(ids => ids).Distinct())
        {
            windows[employeeId] = await EmployeeAvailabilityLoader.GetFreeWindowsAsync(
                _context,
                appointment.TenantId,
                appointment.BranchId,
                employeeId,
                date,
                excludedAppointmentServiceId: null,
                cancellationToken);
        }

        var starts = new HashSet<DateTime>();
        var first = lines[0];
        if (requestedTime != null && requestedTime.Value.Date == date.ToDateTime(TimeOnly.MinValue).Date)
        {
            starts.Add(requestedTime.Value);
        }

        foreach (var employeeId in eligible[first.ServiceId])
        {
            if (!windows.TryGetValue(employeeId, out var free)) continue;
            foreach (var slot in AvailabilityMath.Slots(free, first.DurationMinutes))
            {
                starts.Add(slot.Start);
            }
        }

        var requestedClock = requestedTime?.TimeOfDay ?? TimeSpan.Zero;
        var orderedStarts = starts
            .Select(start => new { start, gap = Math.Abs((start.TimeOfDay - requestedClock).TotalMinutes) })
            .OrderBy(item => item.gap)
            .Take(24)
            .Select(item => item.start)
            .ToList();

        var choices = new List<AllocationChoice>();
        foreach (var start in orderedStarts)
        {
            if (TryAssign(lines, eligible, stylistNames, windows, date, start, requestedTime, out var choice))
            {
                choices.Add(choice);
            }
        }

        return choices;
    }

    private static bool TryAssign(
        List<AppointmentService> lines,
        Dictionary<long, List<long>> eligible,
        IReadOnlyDictionary<long, string> stylistNames,
        Dictionary<long, IReadOnlyList<TimeRange>> windows,
        DateOnly date,
        DateTime start,
        DateTime? requestedTime,
        out AllocationChoice choice)
    {
        choice = new AllocationChoice();
        var remaining = windows.ToDictionary(item => item.Key, item => item.Value);
        var cursor = start;
        var assigned = new List<AllocationLineResponse>();
        var preferredHonoured = true;
        var hadPreference = false;
        foreach (var line in lines)
        {
            var end = cursor.AddMinutes(line.DurationMinutes);
            if (DateOnly.FromDateTime(cursor) != date || DateOnly.FromDateTime(end) != date)
            {
                return false;
            }

            long? chosen = null;
            if (line.PreferredEmployeeId is > 0)
            {
                hadPreference = true;
                var preferred = line.PreferredEmployeeId.Value;
                if (eligible[line.ServiceId].Contains(preferred) &&
                    remaining.TryGetValue(preferred, out var preferredWindows) &&
                    AvailabilityMath.Fits(preferredWindows, cursor, end))
                {
                    chosen = preferred;
                }
            }

            if (chosen == null)
            {
                if (line.PreferredEmployeeId is > 0) preferredHonoured = false;
                chosen = eligible[line.ServiceId]
                    .Where(employeeId =>
                        remaining.TryGetValue(employeeId, out var free) &&
                        AvailabilityMath.Fits(free, cursor, end))
                    .OrderBy(employeeId => employeeId)
                    .Cast<long?>()
                    .FirstOrDefault();
            }

            if (chosen is not > 0) return false;
            remaining[chosen.Value] = AvailabilityMath.Subtract(
                remaining[chosen.Value],
                [new TimeRange(cursor, end)]);
            assigned.Add(new AllocationLineResponse
            {
                AppointmentServiceId = line.AppointmentServiceId,
                ServiceId = line.ServiceId,
                ServiceName = line.Service?.ServiceName ?? string.Empty,
                DurationMinutes = line.DurationMinutes,
                EmployeeId = chosen.Value,
                StylistName = stylistNames.TryGetValue(chosen.Value, out var name) ? name : $"Stylist {chosen.Value}",
                StartTime = cursor,
                EndTime = end,
                RowVersion = line.RowVersion.Length == 0 ? string.Empty : SchedulingGuard.WriteRowVersion(line.RowVersion)
            });
            cursor = end;
        }

        var dayGap = Math.Abs(date.DayNumber - (requestedTime == null
            ? date.DayNumber
            : DateOnly.FromDateTime(requestedTime.Value).DayNumber));
        var minuteGap = requestedTime == null
            ? 0
            : (int)Math.Abs((start.TimeOfDay - requestedTime.Value.TimeOfDay).TotalMinutes);
        choice = new AllocationChoice
        {
            FirstStart = start,
            DayDifference = dayGap,
            MinuteDifference = minuteGap,
            UsesPreferredStylist = hadPreference && preferredHonoured,
            Lines = assigned
        };
        return true;
    }

    private async Task<List<long>> StaffStylistIdsAsync(
        long tenantId,
        long branchId,
        long serviceId,
        CancellationToken cancellationToken)
    {
        return await (
            from skill in _context.EmployeeServices.IgnoreQueryFilters().AsNoTracking()
            join employee in _context.Employees.IgnoreQueryFilters().AsNoTracking()
                on skill.EmployeeId equals employee.EmployeeId
            where skill.TenantId == tenantId &&
                  skill.BranchId == branchId &&
                  skill.ServiceId == serviceId &&
                  skill.IsActive &&
                  !skill.IsDeleted &&
                  employee.TenantId == tenantId &&
                  employee.BranchId == branchId &&
                  employee.IsActive &&
                  !employee.IsDeleted
            select employee.EmployeeId
        ).Distinct().ToListAsync(cancellationToken);
    }

    private async Task<Dictionary<long, string>> StylistNamesAsync(
        IEnumerable<long> employeeIds,
        CancellationToken cancellationToken)
    {
        var ids = employeeIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        return await _context.Employees.AsNoTracking()
            .Where(employee => ids.Contains(employee.EmployeeId))
            .ToDictionaryAsync(employee => employee.EmployeeId, employee => employee.FirstName, cancellationToken);
    }
}
