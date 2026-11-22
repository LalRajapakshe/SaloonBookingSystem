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

public sealed partial class AppointmentWorkflowService : IAppointmentWorkflowService
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
        // A branch user stays on that branch. A salon-wide user with no branch
        // on the token can list every appointment in the tenant, or one branch
        // when the request names it.
        long? branchId = _currentUser.BranchId is > 0 || request.BranchId > 0
            ? _currentUser.ResolveBranchId(request.BranchId)
            : null;
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var query = _context.Appointments
            .Where(appointment => appointment.TenantId == tenantId);
        if (branchId is > 0)
        {
            query = query.Where(appointment => appointment.BranchId == branchId);
        }

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
        await AttachCancellationRequestsAsync(pageItems, cancellationToken);
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

    public async Task<IReadOnlyList<CustomerServiceChoiceResponse>> ListOwnServicesAsync(
        CancellationToken cancellationToken = default)
    {
        var customer = await RequireSignedInCustomerAsync(cancellationToken);
        var services = await _context.Services
            .IgnoreQueryFilters()
            .Where(service =>
                service.TenantId == customer.TenantId &&
                service.BranchId == customer.BranchId &&
                service.IsActive &&
                !service.IsDeleted)
            .OrderBy(service => service.ServiceName)
            .Select(service => new CustomerServiceChoiceResponse
            {
                ServiceId = service.ServiceId,
                BranchId = service.BranchId,
                ServiceName = service.ServiceName,
                DurationMinutes = service.DurationMinutes,
                Price = service.Price
            })
            .ToListAsync(cancellationToken);

        return services;
    }

    public async Task<AppointmentResponse> CreateOwnAppointmentAsync(
        CreateOwnAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var customer = await RequireSignedInCustomerAsync(cancellationToken);
        if (request.AppointmentDate == default)
        {
            throw new SchedulingException("Appointment date is required.");
        }

        var branchId = request.BranchId > 0 ? request.BranchId : customer.BranchId;
        var branch = await _context.Branches
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                item => item.BranchId == branchId &&
                    item.TenantId == customer.TenantId &&
                    item.IsActive &&
                    !item.IsDeleted &&
                    item.IsCustomerVisible,
                cancellationToken);
        if (branch == null)
        {
            throw new SchedulingException("That studio is not available.");
        }

        var service = await _context.Services
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                item => item.ServiceId == request.ServiceId &&
                    item.TenantId == customer.TenantId &&
                    item.BranchId == branch.BranchId &&
                    item.IsActive &&
                    !item.IsDeleted &&
                    item.IsCustomerVisible,
                cancellationToken);
        if (service == null)
        {
            throw new SchedulingException("That service is not available at the selected studio.");
        }

        if (request.PreferredStartTime == null)
        {
            throw new SchedulingException("A preferred time is required.");
        }

        DateTime? preferredStart = request.PreferredStartTime;
        DateTime? preferredEnd = preferredStart?.AddMinutes(service.DurationMinutes);
        ValidatePreferredTimes(request.AppointmentDate, preferredStart, preferredEnd);

        var eligible = await EligibleStylistIdsAsync(
            customer.TenantId,
            branch.BranchId,
            service.ServiceId,
            cancellationToken);
        if (request.PreferredEmployeeId is > 0 && !eligible.Contains(request.PreferredEmployeeId.Value))
        {
            throw new SchedulingException("That stylist cannot perform this service.");
        }

        var stylistsToCheck = request.PreferredEmployeeId is > 0
            ? new List<long> { request.PreferredEmployeeId.Value }
            : eligible;
        if (stylistsToCheck.Count == 0)
        {
            throw new SchedulingException("No stylist is available for this service.");
        }

        var stillOpen = false;
        foreach (var employeeId in stylistsToCheck)
        {
            var free = await EmployeeAvailabilityLoader.GetFreeWindowsAsync(
                _context,
                customer.TenantId,
                branch.BranchId,
                employeeId,
                request.AppointmentDate,
                excludedAppointmentServiceId: null,
                cancellationToken);
            if (AvailabilityMath.Fits(free, preferredStart!.Value, preferredEnd!.Value))
            {
                stillOpen = true;
                break;
            }
        }

        if (!stillOpen)
        {
            throw new SchedulingConflictException(
                "That time is no longer available. Choose another time.");
        }

        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        if (notes is { Length: > 1000 })
        {
            notes = notes[..1000];
        }

        var appointment = new Appointment
        {
            TenantId = customer.TenantId,
            BranchId = branch.BranchId,
            CustomerId = customer.CustomerId,
            AppointmentDate = request.AppointmentDate,
            Status = AppointmentStatus.Pending,
            Notes = notes
        };
        _context.Appointments.Add(appointment);
        AddStatusHistory(appointment, null, AppointmentStatus.Pending, CustomerBookingRequest.RequestedReason);

        var line = new AppointmentService
        {
            TenantId = customer.TenantId,
            BranchId = branch.BranchId,
            Appointment = appointment,
            ServiceId = service.ServiceId,
            DurationMinutes = service.DurationMinutes,
            UnitPrice = service.Price,
            Sequence = 1,
            PreferredEmployeeId = request.PreferredEmployeeId is > 0 ? request.PreferredEmployeeId : null,
            PreferredStartTime = preferredStart,
            PreferredEndTime = preferredEnd
        };
        PrepareInsertedRowVersion(line);
        _context.AppointmentServices.Add(line);
        await _context.SaveChangesAsync(cancellationToken);
        return await MapLoadedAppointmentAsync(appointment, cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerOwnAppointmentResponse>> ListOwnAppointmentsAsync(
        CancellationToken cancellationToken = default)
    {
        var customer = await RequireSignedInCustomerAsync(cancellationToken);
        var appointments = await _context.Appointments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(appointment =>
                appointment.CustomerId == customer.CustomerId &&
                appointment.TenantId == customer.TenantId &&
                !appointment.IsDeleted)
            .Include(appointment => appointment.Branch)
            .Include(appointment => appointment.StatusHistory)
            .Include(appointment => appointment.AppointmentServices)
                .ThenInclude(line => line.Service)
            .Include(appointment => appointment.AppointmentServices)
                .ThenInclude(line => line.Employee)
            .OrderByDescending(appointment => appointment.AppointmentDate)
            .ThenByDescending(appointment => appointment.AppointmentId)
            .ToListAsync(cancellationToken);

        return appointments.Select(MapOwnAppointment).ToList();
    }

    public async Task<CustomerOwnAppointmentResponse> RequestOwnCancellationAsync(
        long appointmentId,
        CancellationToken cancellationToken = default)
    {
        var customer = await RequireSignedInCustomerAsync(cancellationToken);
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var appointment = await LockOwnAppointmentAsync(appointmentId, customer, cancellationToken);
        if (appointment.Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled or AppointmentStatus.NoShow)
        {
            throw new SchedulingException("This appointment can no longer be cancelled.");
        }

        if (appointment.AppointmentDate < SalonTime.Today())
        {
            throw new SchedulingException("This appointment date has passed and can no longer be cancelled.");
        }

        if (CancellationState(appointment.StatusHistory) == CustomerCancellationRequest.Requested)
        {
            throw new SchedulingException("A cancellation request is already waiting for the salon.");
        }

        AddStatusHistory(
            appointment,
            appointment.Status,
            appointment.Status,
            CustomerCancellationRequest.RequestedReason);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await LoadOwnAppointmentAsync(appointment.AppointmentId, customer, cancellationToken);
    }

    public async Task<IReadOnlyList<CancellationRequestNotificationResponse>> ListStaffNotificationsAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        long? branchId = _currentUser.BranchId is > 0 ? _currentUser.BranchId : null;
        var historyRows = await (
            from history in _context.AppointmentStatusHistories.AsNoTracking()
            join appointment in _context.Appointments.AsNoTracking()
                on history.AppointmentId equals appointment.AppointmentId
            where appointment.TenantId == tenantId &&
                  (branchId == null || appointment.BranchId == branchId.Value) &&
                  (history.Reason == CustomerCancellationRequest.RequestedReason ||
                   history.Reason == CustomerBookingRequest.RequestedReason)
            select appointment.AppointmentId).Distinct().ToListAsync(cancellationToken);
        if (historyRows.Count == 0) return [];

        var appointments = await _context.Appointments
            .AsNoTracking()
            .Where(appointment => historyRows.Contains(appointment.AppointmentId))
            .Include(appointment => appointment.Customer)
            .Include(appointment => appointment.Branch)
            .Include(appointment => appointment.StatusHistory)
            .Include(appointment => appointment.AppointmentServices)
                .ThenInclude(line => line.Service)
            .ToListAsync(cancellationToken);

        var stylistIds = appointments
            .SelectMany(appointment => appointment.AppointmentServices)
            .Where(line => line.PreferredEmployeeId is > 0)
            .Select(line => line.PreferredEmployeeId!.Value)
            .Distinct()
            .ToList();
        var stylistNames = stylistIds.Count == 0
            ? new Dictionary<long, string>()
            : await _context.Employees.AsNoTracking()
                .Where(employee => stylistIds.Contains(employee.EmployeeId))
                .ToDictionaryAsync(employee => employee.EmployeeId, employee => employee.FirstName, cancellationToken);

        var notices = new List<CancellationRequestNotificationResponse>();
        foreach (var appointment in appointments)
        {
            var cancellation = CancellationState(appointment.StatusHistory);
            if (cancellation == CustomerCancellationRequest.Requested)
            {
                notices.Add(DescribeNotice(
                    appointment,
                    "CancellationRequest",
                    stylistNames,
                    cancellationRequestedAt: true));
                continue;
            }

            if (!IsOpenBooking(appointment.Status)) continue;
            if (!appointment.StatusHistory.Any(item =>
                    !item.IsDeleted && item.Reason == CustomerBookingRequest.RequestedReason))
            {
                continue;
            }

            var openLines = OpenLines(appointment);
            if (openLines.Count == 0 || openLines.All(line =>
                    SchedulingStateCalculator.IsFullyScheduled(line.EmployeeId, line.StartTime, line.EndTime, false)))
            {
                continue;
            }
            notices.Add(DescribeNotice(appointment, CustomerBookingRequest.NotificationType, stylistNames, cancellationRequestedAt: false));
        }

        return notices
            .OrderByDescending(item => item.NotificationType == CustomerBookingRequest.NotificationType)
            .ThenByDescending(item => item.RequestedAt)
            .ToList();
    }

    private static bool IsOpenBooking(AppointmentStatus status) =>
        status is AppointmentStatus.Pending
            or AppointmentStatus.Confirmed
            or AppointmentStatus.CheckedIn
            or AppointmentStatus.InProgress;

    private static List<AppointmentService> OpenLines(Appointment appointment) =>
        appointment.AppointmentServices
            .Where(line => !line.IsDeleted && !line.IsCancelled)
            .OrderBy(line => line.Sequence)
            .ToList();

    private static CancellationRequestNotificationResponse DescribeNotice(
        Appointment appointment,
        string notificationType,
        IReadOnlyDictionary<long, string> stylistNames,
        bool cancellationRequestedAt)
    {
        var lines = OpenLines(appointment);
        var names = lines
            .Select(line => line.Service?.ServiceName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToList();
        var when = notificationType == CustomerBookingRequest.NotificationType
            ? lines.Select(line => line.PreferredStartTime).FirstOrDefault(time => time != null)
            : lines.Select(line => line.StartTime ?? line.PreferredStartTime).FirstOrDefault(time => time != null);
        var requestedAt = cancellationRequestedAt
            ? appointment.StatusHistory
                .Where(item =>
                    !item.IsDeleted &&
                    item.OldStatus == item.NewStatus &&
                    item.Reason == CustomerCancellationRequest.RequestedReason)
                .OrderByDescending(item => item.ChangedAt)
                .ThenByDescending(item => item.AppointmentStatusHistoryId)
                .Select(item => item.ChangedAt)
                .FirstOrDefault()
            : appointment.CreatedDate;
        var preferredId = lines.Select(line => line.PreferredEmployeeId).FirstOrDefault(id => id is > 0);
        var customerName = $"{appointment.Customer?.FirstName} {appointment.Customer?.LastName}".Trim();
        var scheduledCount = lines.Count(line => SchedulingStateCalculator.IsFullyScheduled(
            line.EmployeeId, line.StartTime, line.EndTime, line.IsCancelled));
        var allocation = scheduledCount == 0
            ? CustomerBookingRequest.Unallocated
            : scheduledCount == lines.Count
                ? SchedulingStateCalculator.Scheduled
                : CustomerBookingRequest.PartiallyScheduled;
        return new CancellationRequestNotificationResponse
        {
            NotificationType = notificationType,
            AppointmentId = appointment.AppointmentId,
            CustomerName = string.IsNullOrWhiteSpace(customerName)
                ? $"Customer {appointment.CustomerId}"
                : customerName,
            ServiceNames = names,
            ServiceName = names.Count == 0 ? string.Empty : string.Join(", ", names),
            AppointmentDate = appointment.AppointmentDate,
            AppointmentTime = when,
            BranchId = appointment.BranchId,
            BranchName = appointment.Branch?.BranchName ?? string.Empty,
            RequestedAt = requestedAt,
            RequestState = notificationType == CustomerBookingRequest.NotificationType
                ? allocation
                : CustomerCancellationRequest.Requested,
            PreferredStylistName = preferredId is > 0 && stylistNames.TryGetValue(preferredId.Value, out var stylist)
                ? stylist
                : null,
            AllocationState = allocation
        };
    }

    public async Task<AppointmentResponse> DeclineCancellationRequestAsync(
        long appointmentId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var appointment = await LockStaffAppointmentAsync(appointmentId, cancellationToken);
        if (CancellationState(appointment.StatusHistory) != CustomerCancellationRequest.Requested)
        {
            throw new SchedulingException("There is no cancellation request to decline.");
        }

        AddStatusHistory(
            appointment,
            appointment.Status,
            appointment.Status,
            CustomerCancellationRequest.DeclinedReason);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await MapLoadedAppointmentAsync(appointment, cancellationToken);
    }

    public async Task<AppointmentResponse> ApproveCancellationRequestAsync(
        long appointmentId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var appointment = await LockStaffAppointmentAsync(appointmentId, cancellationToken);
        if (appointment.Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled or AppointmentStatus.NoShow)
        {
            throw new SchedulingException("This appointment can no longer be cancelled.");
        }

        if (CancellationState(appointment.StatusHistory) != CustomerCancellationRequest.Requested)
        {
            throw new SchedulingException("There is no cancellation request to approve.");
        }

        var previous = appointment.Status;
        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelledAt = DateTime.UtcNow;
        appointment.CancelledByUserId = _currentUser.UserId;
        appointment.CancellationReason = CustomerCancellationRequest.ApprovedReason;
        AddStatusHistory(appointment, previous, AppointmentStatus.Cancelled, CustomerCancellationRequest.ApprovedReason);

        var lines = await LoadLinesAsync(appointment.AppointmentId, cancellationToken);
        foreach (var line in lines.Where(line => !line.IsCancelled))
        {
            line.IsCancelled = true;
            TouchRowVersion(line);
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await MapLoadedAppointmentAsync(appointment, cancellationToken);
    }

    private async Task<CustomerOwnAppointmentResponse> LoadOwnAppointmentAsync(
        long appointmentId,
        Customer customer,
        CancellationToken cancellationToken)
    {
        var appointment = await _context.Appointments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(item => item.Branch)
            .Include(item => item.StatusHistory)
            .Include(item => item.AppointmentServices)
                .ThenInclude(line => line.Service)
            .Include(item => item.AppointmentServices)
                .ThenInclude(line => line.Employee)
            .FirstAsync(item =>
                item.AppointmentId == appointmentId &&
                item.CustomerId == customer.CustomerId &&
                !item.IsDeleted,
                cancellationToken);
        return MapOwnAppointment(appointment);
    }

    private static CustomerOwnAppointmentResponse MapOwnAppointment(Appointment appointment)
    {
        var lines = appointment.AppointmentServices
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Sequence)
            .ToList();
        var line = lines.FirstOrDefault();
        var names = lines
            .Select(item => item.Service?.ServiceName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToList();
        return new CustomerOwnAppointmentResponse
        {
            AppointmentId = appointment.AppointmentId,
            BranchId = appointment.BranchId,
            BranchName = appointment.Branch?.BranchName ?? string.Empty,
            ServiceId = line?.ServiceId ?? 0,
            ServiceName = names.Count == 0 ? string.Empty : string.Join(", ", names),
            ServiceNames = names,
            AppointmentDate = appointment.AppointmentDate,
            PreferredStartTime = line?.PreferredStartTime,
            ScheduledStartTime = line?.StartTime,
            StylistName = lines.Select(item => item.Employee?.FirstName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)),
            Status = appointment.Status.ToString(),
            CancellationRequest = CancellationState(appointment.StatusHistory),
            CreatedDate = appointment.CreatedDate
        };
    }

    private async Task AttachCancellationRequestsAsync(
        List<AppointmentResponse> appointments,
        CancellationToken cancellationToken)
    {
        var ids = appointments.Select(item => item.AppointmentId).ToList();
        if (ids.Count == 0) return;
        var history = await _context.AppointmentStatusHistories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => ids.Contains(item.AppointmentId) && !item.IsDeleted)
            .Select(item => new
            {
                item.AppointmentId,
                item.AppointmentStatusHistoryId,
                item.ChangedAt,
                item.OldStatus,
                item.NewStatus,
                item.Reason
            })
            .ToListAsync(cancellationToken);
        foreach (var appointment in appointments)
        {
            appointment.CancellationRequest = CustomerCancellationRequest.FromHistory(
                history.Where(item => item.AppointmentId == appointment.AppointmentId)
                    .Select(item => new CancellationHistoryEntry(
                        item.ChangedAt,
                        item.AppointmentStatusHistoryId,
                        item.OldStatus,
                        item.NewStatus,
                        item.Reason)));
        }
    }

    private async Task<List<long>> EligibleStylistIdsAsync(
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
                  !employee.IsDeleted &&
                  employee.IsCustomerVisible
            select employee.EmployeeId
        ).Distinct().ToListAsync(cancellationToken);
    }

    private static string CancellationState(IEnumerable<AppointmentStatusHistory> history) =>
        CustomerCancellationRequest.FromHistory(history.Select(item => new CancellationHistoryEntry(
            item.ChangedAt,
            item.AppointmentStatusHistoryId,
            item.OldStatus,
            item.NewStatus,
            item.Reason)));

    private async Task LockAppointmentRowAsync(long appointmentId, CancellationToken cancellationToken)
    {
        var stamp = DateTime.UtcNow;
        var updated = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Appointments SET ModifiedDate = {stamp} WHERE AppointmentId = {appointmentId} AND IsDeleted = 0",
            cancellationToken);
        if (updated == 0)
        {
            throw new SchedulingException("The appointment could not be found.");
        }
    }

    private async Task<Appointment> LockOwnAppointmentAsync(
        long appointmentId,
        Customer customer,
        CancellationToken cancellationToken)
    {
        var visible = await _context.Appointments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(item =>
                item.AppointmentId == appointmentId &&
                item.CustomerId == customer.CustomerId &&
                item.TenantId == customer.TenantId &&
                !item.IsDeleted,
                cancellationToken);
        if (!visible)
        {
            throw new SchedulingException("The appointment could not be found.");
        }

        await LockAppointmentRowAsync(appointmentId, cancellationToken);
        _context.ChangeTracker.Clear();
        return await _context.Appointments
            .IgnoreQueryFilters()
            .Include(item => item.StatusHistory)
            .FirstAsync(item =>
                item.AppointmentId == appointmentId &&
                item.CustomerId == customer.CustomerId &&
                !item.IsDeleted,
                cancellationToken);
    }

    private async Task<Appointment> LockStaffAppointmentAsync(
        long appointmentId,
        CancellationToken cancellationToken)
    {
        var existing = await RequireAppointmentAsync(appointmentId, cancellationToken);
        await LockAppointmentRowAsync(existing.AppointmentId, cancellationToken);
        _context.ChangeTracker.Clear();
        return await _context.Appointments
            .IgnoreQueryFilters()
            .Include(item => item.StatusHistory)
            .FirstAsync(item => item.AppointmentId == existing.AppointmentId && !item.IsDeleted, cancellationToken);
    }

    private async Task<Customer> RequireSignedInCustomerAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.UserCategory != UserCategory.Customer || _currentUser.CustomerId is not > 0)
        {
            throw new UnauthorizedAccessException("A customer sign-in is required.");
        }

        var tenantId = _currentUser.RequireTenantId();
        var customer = await _context.Customers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                item => item.CustomerId == _currentUser.CustomerId &&
                    item.TenantId == tenantId &&
                    item.IsActive &&
                    !item.IsDeleted,
                cancellationToken);
        if (customer == null)
        {
            throw new UnauthorizedAccessException("The signed-in customer was not found.");
        }

        return customer;
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

        if (appointment.Status != AppointmentStatus.Pending)
        {
            throw new SchedulingException(
                "The appointment can be edited only before the customer confirms it.");
        }

        var lines = await LoadLinesAsync(appointment.AppointmentId, cancellationToken);
        if (lines.Any(line => !line.IsCancelled && line.StartTime != null))
        {
            throw new SchedulingException(
                "The appointment can be edited only before a time slot is allocated.");
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
            Notes = request.Notes,
            PreferredStartTime = request.PreferredStartTime,
            PreferredEndTime = request.PreferredEndTime
        };
        ValidatePreferredTimes(appointment.AppointmentDate, line.PreferredStartTime, line.PreferredEndTime);
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
                appointment.TenantId,
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
            CancellationRequest = CancellationState(statusHistory),
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

    private static void ValidatePreferredTimes(DateOnly appointmentDate, DateTime? start, DateTime? end)
    {
        if (start is null && end is null)
        {
            return;
        }

        if (start is null || end is null)
        {
            throw new SchedulingException("Enter both preferred start and preferred finish.");
        }

        if (end <= start)
        {
            throw new SchedulingException("Preferred finish must be after preferred start.");
        }

        if (DateOnly.FromDateTime(start.Value) != appointmentDate ||
            DateOnly.FromDateTime(end.Value) != appointmentDate)
        {
            throw new SchedulingException("The preferred time must be on the appointment date.");
        }
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
            PreferredStartTime = line.PreferredStartTime,
            PreferredEndTime = line.PreferredEndTime,
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
