using System.Data;
using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Scheduling;
using SalonBooking.Application.Interfaces;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Infrastructure.Scheduling;
using SalonBooking.Infrastructure.Security;
using SalonBooking.Persistence.Context;

namespace SalonBooking.Infrastructure.Services;

public sealed class AppointmentWorkflowService : IAppointmentWorkflowService
{
    private readonly SalonBookingDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AppointmentWorkflowService(
        SalonBookingDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<AppointmentResponse>> GetAppointmentsAsync(
        AppointmentQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var branchId = _currentUser.ResolveBranchId(request.BranchId);
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var query = _context.Appointments
            .Where(appointment => appointment.TenantId == tenantId && appointment.BranchId == branchId);

        if (request.Date is DateOnly date)
        {
            query = query.Where(appointment => appointment.AppointmentDate == date);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = SchedulingGuard.ParseStatus(request.Status, AppointmentStatus.Pending);
            query = query.Where(appointment => appointment.Status == status);
        }

        var appointments = await query
            .Include(appointment => appointment.AppointmentServices)
            .OrderBy(appointment => appointment.AppointmentDate)
            .ThenBy(appointment => appointment.AppointmentId)
            .ToListAsync(cancellationToken);

        var mapped = appointments
            .Select(appointment => MapAppointment(appointment, appointment.AppointmentServices, []))
            .ToList();

        if (request.NeedsScheduling == true)
        {
            mapped = mapped.Where(appointment => appointment.NeedsScheduling).ToList();
        }

        var pageItems = mapped.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<AppointmentResponse>
        {
            Items = pageItems,
            Page = page,
            PageSize = pageSize,
            TotalRecords = mapped.Count,
            TotalPages = mapped.Count == 0 ? 0 : (int)Math.Ceiling(mapped.Count / (double)pageSize)
        };
    }

    public async Task<AppointmentResponse> GetAppointmentAsync(
        long appointmentId,
        CancellationToken cancellationToken = default)
    {
        var appointment = await RequireAppointmentAsync(appointmentId, cancellationToken);
        return await MapLoadedAppointmentAsync(appointment, cancellationToken);
    }

    public async Task<AppointmentResponse> CreateAppointmentAsync(
        CreateAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var branchId = _currentUser.ResolveBranchId(request.BranchId);
        await SchedulingGuard.RequireBranchAsync(_context, tenantId, branchId, cancellationToken);
        await SchedulingGuard.RequireCustomerAsync(
            _context, _currentUser, tenantId, branchId, request.CustomerId, cancellationToken);

        if (request.AppointmentDate == default)
        {
            throw new SchedulingException("Appointment date is required.");
        }

        var status = SchedulingGuard.ParseStatus(request.Status, AppointmentStatus.Pending);
        var appointment = new Appointment
        {
            TenantId = tenantId,
            BranchId = branchId,
            CustomerId = request.CustomerId,
            AppointmentDate = request.AppointmentDate,
            Status = status,
            Notes = request.Notes
        };

        if (status == AppointmentStatus.Cancelled)
        {
            appointment.CancelledAt = DateTime.UtcNow;
            appointment.CancelledByUserId = _currentUser.UserId;
            appointment.CancellationReason = request.Notes;
        }

        _context.Appointments.Add(appointment);
        AddStatusHistory(appointment, null, status, "Appointment created");
        await _context.SaveChangesAsync(cancellationToken);
        return await MapLoadedAppointmentAsync(appointment, cancellationToken);
    }

    public async Task<AppointmentResponse> UpdateAppointmentAsync(
        long appointmentId,
        UpdateAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var appointment = await RequireAppointmentAsync(appointmentId, cancellationToken);
        if (request.AppointmentDate == default)
        {
            throw new SchedulingException("Appointment date is required.");
        }

        var lines = await LoadLinesAsync(appointment.AppointmentId, cancellationToken);
        if (request.AppointmentDate != appointment.AppointmentDate &&
            lines.Any(line => !line.IsCancelled && line.StartTime != null))
        {
            throw new SchedulingException("Reschedule the service lines before changing the appointment date.");
        }

        appointment.AppointmentDate = request.AppointmentDate;
        appointment.Notes = request.Notes;
        await _context.SaveChangesAsync(cancellationToken);
        return await MapLoadedAppointmentAsync(appointment, cancellationToken);
    }

    public async Task<AppointmentResponse> ChangeStatusAsync(
        long appointmentId,
        ChangeAppointmentStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var appointment = await RequireAppointmentAsync(appointmentId, cancellationToken);
        var status = SchedulingGuard.ParseStatus(request.Status, appointment.Status);
        if (string.IsNullOrWhiteSpace(request.Status))
        {
            throw new SchedulingException("Appointment status is required.");
        }

        if (status == AppointmentStatus.Cancelled)
        {
            return await CancelAppointmentAsync(
                appointmentId,
                new CancelAppointmentRequest { Reason = request.Reason },
                cancellationToken);
        }

        if (appointment.Status == status)
        {
            throw new SchedulingException("The appointment already has that status.");
        }

        var previous = appointment.Status;
        appointment.Status = status;
        appointment.CancelledAt = null;
        appointment.CancelledByUserId = null;
        appointment.CancellationReason = null;
        AddStatusHistory(appointment, previous, status, request.Reason);
        await _context.SaveChangesAsync(cancellationToken);
        return await MapLoadedAppointmentAsync(appointment, cancellationToken);
    }

    public async Task<AppointmentResponse> CancelAppointmentAsync(
        long appointmentId,
        CancelAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var appointment = await RequireAppointmentAsync(appointmentId, cancellationToken);
        if (appointment.Status == AppointmentStatus.Cancelled)
        {
            throw new SchedulingException("The appointment is already cancelled.");
        }

        var previous = appointment.Status;
        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelledAt = DateTime.UtcNow;
        appointment.CancelledByUserId = _currentUser.UserId;
        appointment.CancellationReason = request.Reason;
        AddStatusHistory(appointment, previous, AppointmentStatus.Cancelled, request.Reason);

        var lines = await LoadLinesAsync(appointment.AppointmentId, cancellationToken);
        foreach (var line in lines.Where(line => !line.IsCancelled))
        {
            line.IsCancelled = true;
            TouchRowVersion(line);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await MapLoadedAppointmentAsync(appointment, cancellationToken);
    }

    public async Task<AppointmentServiceResponse> AddServiceAsync(
        long appointmentId,
        AddAppointmentServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var appointment = await RequireOpenAppointmentAsync(appointmentId, cancellationToken);
        var service = await SchedulingGuard.RequireServiceAsync(
            _context,
            _currentUser,
            appointment.TenantId,
            appointment.BranchId,
            request.ServiceId,
            cancellationToken);

        var lines = await LoadLinesAsync(appointment.AppointmentId, cancellationToken);
        var line = new AppointmentService
        {
            TenantId = appointment.TenantId,
            BranchId = appointment.BranchId,
            AppointmentId = appointment.AppointmentId,
            ServiceId = service.ServiceId,
            DurationMinutes = service.DurationMinutes,
            UnitPrice = service.Price,
            Sequence = lines.Count == 0 ? 1 : lines.Max(item => item.Sequence) + 1,
            Notes = request.Notes
        };
        PrepareInsertedRowVersion(line);
        _context.AppointmentServices.Add(line);
        await _context.SaveChangesAsync(cancellationToken);
        return MapLine(line, []);
    }

    public async Task<AppointmentServiceResponse> UpdateServiceAsync(
        long appointmentId,
        long appointmentServiceId,
        UpdateAppointmentServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var line = await RequireLineAsync(appointmentId, appointmentServiceId, cancellationToken);
        ApplyRowVersion(line, request.RowVersion);
        if (line.IsCancelled)
        {
            throw new SchedulingException("A cancelled service line cannot be changed.");
        }

        line.Notes = request.Notes;
        TouchRowVersion(line);
        await SaveProtectingConcurrencyAsync(cancellationToken);
        return MapLine(line, await LoadScheduleHistoryAsync(line.AppointmentServiceId, cancellationToken));
    }

    public async Task<AppointmentServiceResponse> CancelServiceAsync(
        long appointmentId,
        long appointmentServiceId,
        string rowVersion,
        CancellationToken cancellationToken = default)
    {
        var line = await RequireLineAsync(appointmentId, appointmentServiceId, cancellationToken);
        ApplyRowVersion(line, rowVersion);
        if (line.IsCancelled)
        {
            throw new SchedulingException("The service line is already cancelled.");
        }

        line.IsCancelled = true;
        TouchRowVersion(line);
        await SaveProtectingConcurrencyAsync(cancellationToken);
        return MapLine(line, await LoadScheduleHistoryAsync(line.AppointmentServiceId, cancellationToken));
    }

    public async Task<AppointmentServiceResponse> ScheduleServiceAsync(
        long appointmentId,
        long appointmentServiceId,
        ScheduleAppointmentServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var appointment = await RequireOpenAppointmentAsync(appointmentId, cancellationToken);
        var line = await RequireLineAsync(appointmentId, appointmentServiceId, cancellationToken);
        ApplyRowVersion(line, request.RowVersion);

        if (line.IsCancelled)
        {
            throw new SchedulingException("A cancelled service line cannot be scheduled.");
        }

        if (request.EndTime <= request.StartTime)
        {
            throw new SchedulingException("End time must be after start time.");
        }

        if (DateOnly.FromDateTime(request.StartTime) != appointment.AppointmentDate ||
            DateOnly.FromDateTime(request.EndTime) != appointment.AppointmentDate)
        {
            throw new SchedulingException("The service must be scheduled on the appointment date.");
        }

        if ((int)(request.EndTime - request.StartTime).TotalMinutes != line.DurationMinutes)
        {
            throw new SchedulingException("The scheduled length must match the service duration.");
        }

        await SchedulingGuard.RequireEmployeeAsync(
            _context,
            _currentUser,
            appointment.TenantId,
            appointment.BranchId,
            request.EmployeeId,
            cancellationToken);

        var capable = await _context.EmployeeServices.AnyAsync(
            capability =>
                capability.EmployeeId == request.EmployeeId &&
                capability.ServiceId == line.ServiceId &&
                capability.BranchId == appointment.BranchId &&
                capability.IsActive,
            cancellationToken);

        if (!capable)
        {
            throw new SchedulingException("The employee is not able to perform this service.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var freeWindows = await EmployeeAvailabilityLoader.GetFreeWindowsAsync(
            _context,
            appointment.BranchId,
            request.EmployeeId,
            appointment.AppointmentDate,
            line.AppointmentServiceId,
            cancellationToken);

        if (!AvailabilityMath.Fits(freeWindows, request.StartTime, request.EndTime))
        {
            throw new SchedulingConflictException(
                "The employee is not available for that time.");
        }

        var changed = line.EmployeeId != request.EmployeeId ||
            line.StartTime != request.StartTime ||
            line.EndTime != request.EndTime;

        if (changed)
        {
            _context.AppointmentServiceScheduleHistories.Add(new AppointmentServiceScheduleHistory
            {
                TenantId = appointment.TenantId,
                BranchId = appointment.BranchId,
                AppointmentServiceId = line.AppointmentServiceId,
                OldEmployeeId = line.EmployeeId,
                NewEmployeeId = request.EmployeeId,
                OldStartTime = line.StartTime,
                NewStartTime = request.StartTime,
                OldEndTime = line.EndTime,
                NewEndTime = request.EndTime,
                ChangedAt = DateTime.UtcNow,
                ChangedByUserId = _currentUser.UserId,
                Reason = request.Reason
            });
        }

        line.EmployeeId = request.EmployeeId;
        line.StartTime = request.StartTime;
        line.EndTime = request.EndTime;
        TouchRowVersion(line);
        await SaveProtectingConcurrencyAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MapLine(line, await LoadScheduleHistoryAsync(line.AppointmentServiceId, cancellationToken));
    }

    private async Task<Appointment> RequireAppointmentAsync(
        long appointmentId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.RequireTenantId();
        return await _context.Appointments.RequireOwnedAsync(
            _currentUser,
            appointment => appointment.AppointmentId == appointmentId && appointment.TenantId == tenantId,
            appointment => appointment.BranchId,
            cancellationToken);
    }

    private async Task<Appointment> RequireOpenAppointmentAsync(
        long appointmentId,
        CancellationToken cancellationToken)
    {
        var appointment = await RequireAppointmentAsync(appointmentId, cancellationToken);
        if (appointment.Status is AppointmentStatus.Cancelled
            or AppointmentStatus.Completed
            or AppointmentStatus.NoShow)
        {
            throw new SchedulingException("Services cannot be changed on a closed appointment.");
        }

        return appointment;
    }

    private async Task<AppointmentService> RequireLineAsync(
        long appointmentId,
        long appointmentServiceId,
        CancellationToken cancellationToken)
    {
        var appointment = await RequireAppointmentAsync(appointmentId, cancellationToken);
        var line = await _context.AppointmentServices
            .FirstOrDefaultAsync(
                item => item.AppointmentServiceId == appointmentServiceId &&
                    item.AppointmentId == appointment.AppointmentId,
                cancellationToken);

        if (line == null)
        {
            throw new KeyNotFoundException();
        }

        return line;
    }

    private async Task<List<AppointmentService>> LoadLinesAsync(
        long appointmentId,
        CancellationToken cancellationToken)
    {
        return await _context.AppointmentServices
            .Where(line => line.AppointmentId == appointmentId)
            .OrderBy(line => line.Sequence)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<AppointmentStatusHistory>> LoadStatusHistoryAsync(
        long appointmentId,
        CancellationToken cancellationToken)
    {
        return await _context.AppointmentStatusHistories
            .Where(item => item.AppointmentId == appointmentId)
            .OrderBy(item => item.ChangedAt)
            .ThenBy(item => item.AppointmentStatusHistoryId)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<AppointmentServiceScheduleHistory>> LoadScheduleHistoryAsync(
        long appointmentServiceId,
        CancellationToken cancellationToken)
    {
        return await _context.AppointmentServiceScheduleHistories
            .Where(item => item.AppointmentServiceId == appointmentServiceId)
            .OrderBy(item => item.ChangedAt)
            .ThenBy(item => item.AppointmentServiceScheduleHistoryId)
            .ToListAsync(cancellationToken);
    }

    private async Task<AppointmentResponse> MapLoadedAppointmentAsync(
        Appointment appointment,
        CancellationToken cancellationToken)
    {
        var lines = await LoadLinesAsync(appointment.AppointmentId, cancellationToken);
        var history = await LoadStatusHistoryAsync(appointment.AppointmentId, cancellationToken);
        var lineIds = lines.Select(line => line.AppointmentServiceId).ToArray();
        var scheduleHistory = await _context.AppointmentServiceScheduleHistories
            .Where(item => lineIds.Contains(item.AppointmentServiceId))
            .ToListAsync(cancellationToken);

        return MapAppointment(appointment, lines, history, scheduleHistory);
    }

    private void AddStatusHistory(
        Appointment appointment,
        AppointmentStatus? oldStatus,
        AppointmentStatus newStatus,
        string? reason)
    {
        _context.AppointmentStatusHistories.Add(new AppointmentStatusHistory
        {
            TenantId = appointment.TenantId,
            BranchId = appointment.BranchId,
            AppointmentId = appointment.AppointmentId,
            Appointment = appointment,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedAt = DateTime.UtcNow,
            ChangedByUserId = _currentUser.UserId,
            Reason = reason
        });
    }

    private void ApplyRowVersion(AppointmentService line, string rowVersion)
    {
        _context.Entry(line).Property(item => item.RowVersion).OriginalValue =
            SchedulingGuard.ReadRowVersion(rowVersion);
    }

    private void PrepareInsertedRowVersion(AppointmentService line)
    {
        if (!_context.Database.IsSqlServer())
        {
            line.RowVersion = Guid.NewGuid().ToByteArray();
        }
    }

    private void TouchRowVersion(AppointmentService line)
    {
        if (!_context.Database.IsSqlServer())
        {
            line.RowVersion = Guid.NewGuid().ToByteArray();
        }
    }

    private async Task SaveProtectingConcurrencyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new SchedulingConflictException(
                "The service line was changed by someone else. Reload it and try again.");
        }
    }

    private static AppointmentResponse MapAppointment(
        Appointment appointment,
        IEnumerable<AppointmentService> lines,
        IEnumerable<AppointmentStatusHistory> statusHistory,
        IEnumerable<AppointmentServiceScheduleHistory>? scheduleHistory = null)
    {
        var lineList = lines.OrderBy(line => line.Sequence).ToList();
        var scheduleLookup = (scheduleHistory ?? [])
            .GroupBy(item => item.AppointmentServiceId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var mappedLines = lineList
            .Select(line => MapLine(
                line,
                scheduleLookup.TryGetValue(line.AppointmentServiceId, out var history)
                    ? history
                    : []))
            .ToList();
        var state = SchedulingStateCalculator.Calculate(mappedLines.Select(line =>
            new ScheduleLineState(line.IsCancelled, line.IsFullyScheduled)));

        return new AppointmentResponse
        {
            AppointmentId = appointment.AppointmentId,
            TenantId = appointment.TenantId,
            BranchId = appointment.BranchId,
            CustomerId = appointment.CustomerId,
            AppointmentDate = appointment.AppointmentDate,
            Status = appointment.Status.ToString(),
            SchedulingState = state,
            NeedsScheduling = SchedulingStateCalculator.NeedsScheduling(appointment.Status, state),
            Notes = appointment.Notes,
            CancelledAt = appointment.CancelledAt,
            CancelledByUserId = appointment.CancelledByUserId,
            CancellationReason = appointment.CancellationReason,
            Services = mappedLines,
            StatusHistory = statusHistory
                .OrderBy(item => item.ChangedAt)
                .Select(item => new AppointmentStatusHistoryResponse
                {
                    AppointmentStatusHistoryId = item.AppointmentStatusHistoryId,
                    OldStatus = item.OldStatus?.ToString(),
                    NewStatus = item.NewStatus.ToString(),
                    ChangedAt = item.ChangedAt,
                    ChangedByUserId = item.ChangedByUserId,
                    Reason = item.Reason
                })
                .ToList()
        };
    }

    private static AppointmentServiceResponse MapLine(
        AppointmentService line,
        IEnumerable<AppointmentServiceScheduleHistory> history)
    {
        return new AppointmentServiceResponse
        {
            AppointmentServiceId = line.AppointmentServiceId,
            AppointmentId = line.AppointmentId,
            BranchId = line.BranchId,
            ServiceId = line.ServiceId,
            EmployeeId = line.EmployeeId,
            StartTime = line.StartTime,
            EndTime = line.EndTime,
            DurationMinutes = line.DurationMinutes,
            UnitPrice = line.UnitPrice,
            Sequence = line.Sequence,
            Notes = line.Notes,
            IsCancelled = line.IsCancelled,
            IsFullyScheduled = SchedulingStateCalculator.IsFullyScheduled(
                line.EmployeeId,
                line.StartTime,
                line.EndTime,
                line.IsCancelled),
            RowVersion = line.RowVersion.Length == 0
                ? string.Empty
                : SchedulingGuard.WriteRowVersion(line.RowVersion),
            ScheduleHistory = history
                .OrderBy(item => item.ChangedAt)
                .Select(item => new AppointmentServiceScheduleHistoryResponse
                {
                    AppointmentServiceScheduleHistoryId = item.AppointmentServiceScheduleHistoryId,
                    OldEmployeeId = item.OldEmployeeId,
                    NewEmployeeId = item.NewEmployeeId,
                    OldStartTime = item.OldStartTime,
                    NewStartTime = item.NewStartTime,
                    OldEndTime = item.OldEndTime,
                    NewEndTime = item.NewEndTime,
                    ChangedAt = item.ChangedAt,
                    ChangedByUserId = item.ChangedByUserId,
                    Reason = item.Reason
                })
                .ToList()
        };
    }
}
