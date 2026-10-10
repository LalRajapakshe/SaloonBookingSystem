using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Scheduling;
using SalonBooking.Application.Interfaces;
using SalonBooking.Domain.Entities;
using SalonBooking.Infrastructure.Scheduling;
using SalonBooking.Infrastructure.Security;
using SalonBooking.Persistence.Context;

namespace SalonBooking.Infrastructure.Services;

public sealed class StaffLeaveService : IStaffLeaveService
{
    private readonly SalonBookingDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public StaffLeaveService(
        SalonBookingDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<StaffLeaveResponse>> GetAsync(
        StaffLeaveQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var branchId = _currentUser.ResolveBranchId(request.BranchId);
        var query = _context.StaffLeaves
            .Where(item => item.TenantId == tenantId && item.BranchId == branchId);

        if (request.EmployeeId is > 0)
        {
            query = query.Where(item => item.EmployeeId == request.EmployeeId);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = SchedulingGuard.ParseLeaveStatus(request.Status);
            query = query.Where(item => item.Status == status);
        }

        var items = await query
            .OrderBy(item => item.StartDateTime)
            .ToListAsync(cancellationToken);

        return items.Select(Map).ToList();
    }

    public async Task<StaffLeaveResponse> CreateAsync(
        SaveStaffLeaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var branchId = _currentUser.ResolveBranchId(request.BranchId);
        await SchedulingGuard.RequireBranchAsync(_context, tenantId, branchId, cancellationToken);
        await SchedulingGuard.RequireEmployeeAsync(
            _context, _currentUser, tenantId, branchId, request.EmployeeId, cancellationToken);
        var (leaveType, status) = Validate(request);

        var leave = new StaffLeave
        {
            TenantId = tenantId,
            BranchId = branchId,
            EmployeeId = request.EmployeeId,
            StartDateTime = request.StartDateTime,
            EndDateTime = request.EndDateTime,
            LeaveType = leaveType,
            Reason = request.Reason,
            Status = status
        };
        _context.StaffLeaves.Add(leave);
        await _context.SaveChangesAsync(cancellationToken);
        return Map(leave);
    }

    public async Task<StaffLeaveResponse> UpdateAsync(
        long staffLeaveId,
        SaveStaffLeaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var leave = await RequireAsync(staffLeaveId, cancellationToken);
        var branchId = _currentUser.ResolveBranchId(request.BranchId);
        if (branchId != leave.BranchId)
        {
            throw new KeyNotFoundException();
        }

        await SchedulingGuard.RequireEmployeeAsync(
            _context, _currentUser, leave.TenantId, leave.BranchId, request.EmployeeId, cancellationToken);
        var (leaveType, status) = Validate(request);
        leave.EmployeeId = request.EmployeeId;
        leave.StartDateTime = request.StartDateTime;
        leave.EndDateTime = request.EndDateTime;
        leave.LeaveType = leaveType;
        leave.Reason = request.Reason;
        leave.Status = status;
        await _context.SaveChangesAsync(cancellationToken);
        return Map(leave);
    }

    public async Task DeleteAsync(
        long staffLeaveId,
        CancellationToken cancellationToken = default)
    {
        var leave = await RequireAsync(staffLeaveId, cancellationToken);
        _context.StaffLeaves.Remove(leave);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static (Domain.Enums.LeaveType Type, Domain.Enums.LeaveStatus Status) Validate(
        SaveStaffLeaveRequest request)
    {
        if (request.EndDateTime <= request.StartDateTime)
        {
            throw new SchedulingException("Leave end must be after leave start.");
        }

        return (SchedulingGuard.ParseLeaveType(request.LeaveType), SchedulingGuard.ParseLeaveStatus(request.Status));
    }

    private async Task<StaffLeave> RequireAsync(
        long staffLeaveId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.RequireTenantId();
        return await _context.StaffLeaves.RequireOwnedAsync(
            _currentUser,
            item => item.StaffLeaveId == staffLeaveId && item.TenantId == tenantId,
            item => item.BranchId,
            cancellationToken);
    }

    private static StaffLeaveResponse Map(StaffLeave leave)
    {
        return new StaffLeaveResponse
        {
            StaffLeaveId = leave.StaffLeaveId,
            TenantId = leave.TenantId,
            BranchId = leave.BranchId,
            EmployeeId = leave.EmployeeId,
            StartDateTime = leave.StartDateTime,
            EndDateTime = leave.EndDateTime,
            LeaveType = leave.LeaveType.ToString(),
            Reason = leave.Reason,
            Status = leave.Status.ToString()
        };
    }
}
