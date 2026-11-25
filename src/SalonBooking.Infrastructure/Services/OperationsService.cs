using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Features.Operations;
using SalonBooking.Application.Features.Scheduling;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Security;
using SalonBooking.Domain.Billing;
using SalonBooking.Domain.Enums;
using SalonBooking.Infrastructure.Authentication;
using SalonBooking.Infrastructure.Scheduling;
using SalonBooking.Infrastructure.Security;
using SalonBooking.Persistence.Context;

namespace SalonBooking.Infrastructure.Services;

public sealed class OperationsService : IOperationsService
{
    private readonly SalonBookingDbContext _context;
    private readonly ICurrentUserService _currentUser;
    public OperationsService(SalonBookingDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<OperationsDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var user = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(
            item => item.UserId == _currentUser.UserId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken)
            ?? throw new UnauthorizedAccessException("User claim is missing.");
        var access = await AccessReader.ReadAccessAsync(_context, user.UserId, tenantId, cancellationToken);
        var permissions = AccessReader.EffectivePermissions(user, access.PermissionCodes).ToHashSet(StringComparer.Ordinal);
        var ownOnly = !permissions.Contains(PermissionCatalog.AppointmentsRead) && permissions.Contains(PermissionCatalog.AppointmentsOwnRead);
        var today = SalonTime.Today();
        var appointments = _context.Appointments.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.AppointmentDate == today);
        if (_currentUser.BranchId is > 0)
        {
            var branchId = _currentUser.BranchId.Value;
            appointments = appointments.Where(item => item.BranchId == branchId);
        }

        if (ownOnly)
        {
            if (_currentUser.EmployeeId is not > 0)
            {
                throw new UnauthorizedAccessException("Employee claim is missing.");
            }

            var employeeId = _currentUser.EmployeeId.Value;
            var ownAppointmentIds = _context.AppointmentServices.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.EmployeeId == employeeId && !item.IsDeleted)
                .Select(item => item.AppointmentId);
            appointments = appointments.Where(item => ownAppointmentIds.Contains(item.AppointmentId));
        }

        var rows = await appointments.Select(item => item.Status).ToListAsync(cancellationToken);
        var response = new OperationsDashboardResponse
        {
            Date = today,
            Scope = ownOnly ? "Mine" : _currentUser.BranchId is > 0 ? "Branch" : "Tenant",
            TodayAppointments = rows.Count,
            Pending = rows.Count(item => item == AppointmentStatus.Pending),
            AwaitingConfirmation = rows.Count(item => item == AppointmentStatus.Pending),
            CheckedIn = rows.Count(item => item == AppointmentStatus.CheckedIn),
            InProgress = rows.Count(item => item == AppointmentStatus.InProgress),
            Completed = rows.Count(item => item == AppointmentStatus.Completed),
            Cancelled = rows.Count(item => item == AppointmentStatus.Cancelled),
            NoShow = rows.Count(item => item == AppointmentStatus.NoShow)
        };

        var lines = _context.AppointmentServices.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && !item.IsCancelled && item.LineStatus != ServiceLineStatus.Completed && item.LineStatus != ServiceLineStatus.Cancelled);
        if (_currentUser.BranchId is > 0)
        {
            var branchId = _currentUser.BranchId.Value;
            lines = lines.Where(item => item.BranchId == branchId);
        }

        if (ownOnly && _currentUser.EmployeeId is > 0)
        {
            var employeeId = _currentUser.EmployeeId.Value;
            response.MyAssignedLines = await lines.CountAsync(item => item.EmployeeId == employeeId, cancellationToken);
            response.OpenServiceLines = response.MyAssignedLines;
        }
        else
        {
            response.OpenServiceLines = await lines.CountAsync(cancellationToken);
            if (_currentUser.EmployeeId is > 0)
            {
                var employeeId = _currentUser.EmployeeId.Value;
                response.MyAssignedLines = await lines.CountAsync(item => item.EmployeeId == employeeId, cancellationToken);
            }
        }

        if (permissions.Contains(PermissionCatalog.RentalsRead) || _currentUser.IsPlatformAdministrator)
        {
            var rentals = _context.RentalReservations.AsNoTracking().Where(item => item.Status == RentalReservationStatus.Issued);
            response.ActiveRentals = await rentals.CountAsync(cancellationToken);
            response.OverdueRentals = await rentals.CountAsync(item => item.PlannedReturnAt < DateTime.UtcNow, cancellationToken);
        }

        if (permissions.Contains(PermissionCatalog.BillsRead) || _currentUser.IsPlatformAdministrator)
        {
            var openBills = await _context.Bills.AsNoTracking()
                .Where(item => item.Status == BillStatus.Finalized && item.PaymentStatus != PaymentStatus.Paid)
                .Select(item => new { item.BillId, item.Total })
                .ToListAsync(cancellationToken);
            response.UnpaidBills = openBills.Count;
            response.OutstandingAmount = 0;
            if (openBills.Count > 0)
            {
                var ids = openBills.Select(item => item.BillId).ToArray();
                var payments = await _context.BillPayments.AsNoTracking()
                    .Where(item => ids.Contains(item.BillId))
                    .Select(item => new { item.BillId, item.Amount })
                    .ToListAsync(cancellationToken);
                var collected = payments
                    .GroupBy(item => item.BillId)
                    .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));
                response.OutstandingAmount = openBills.Sum(bill =>
                    Money.Outstanding(bill.Total, collected.GetValueOrDefault(bill.BillId)));
            }
        }

        return response;
    }
}
