using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Scheduling;
using SalonBooking.Application.Interfaces;
using SalonBooking.Domain.Entities;
using SalonBooking.Infrastructure.Scheduling;
using SalonBooking.Infrastructure.Security;
using SalonBooking.Persistence.Context;

namespace SalonBooking.Infrastructure.Services;

public sealed class StaffScheduleService : IStaffScheduleService
{
    private readonly SalonBookingDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public StaffScheduleService(
        SalonBookingDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<StaffScheduleResponse>> GetAsync(
        StaffScheduleQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var branchId = _currentUser.ResolveBranchId(request.BranchId);
        var query = _context.StaffSchedules
            .Where(item => item.TenantId == tenantId && item.BranchId == branchId);

        if (request.EmployeeId is > 0)
        {
            query = query.Where(item => item.EmployeeId == request.EmployeeId);
        }

        if (request.DayOfWeek is byte day)
        {
            query = query.Where(item => item.DayOfWeek == day);
        }

        var items = await query
            .OrderBy(item => item.EmployeeId)
            .ThenBy(item => item.DayOfWeek)
            .ThenBy(item => item.StartTime)
            .ToListAsync(cancellationToken);

        return items.Select(Map).ToList();
    }

    public async Task<StaffScheduleResponse> CreateAsync(
        SaveStaffScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var branchId = _currentUser.ResolveBranchId(request.BranchId);
        await SchedulingGuard.RequireBranchAsync(_context, tenantId, branchId, cancellationToken);
        await SchedulingGuard.RequireEmployeeAsync(
            _context, _currentUser, tenantId, branchId, request.EmployeeId, cancellationToken);
        var segmentType = Validate(request);

        var segment = new StaffSchedule
        {
            TenantId = tenantId,
            BranchId = branchId,
            EmployeeId = request.EmployeeId,
            DayOfWeek = request.DayOfWeek,
            SegmentType = segmentType,
            StartTime = request.StartTime,
            EndTime = request.EndTime
        };

        await EnsureNoOverlapAsync(segment, null, cancellationToken);
        _context.StaffSchedules.Add(segment);
        await _context.SaveChangesAsync(cancellationToken);

        if (!request.IsActive)
        {
            segment.IsActive = false;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Map(segment);
    }

    public async Task<StaffScheduleResponse> UpdateAsync(
        long staffScheduleId,
        SaveStaffScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        var segment = await RequireAsync(staffScheduleId, cancellationToken);
        var branchId = _currentUser.ResolveBranchId(request.BranchId);
        if (branchId != segment.BranchId)
        {
            throw new KeyNotFoundException();
        }

        await SchedulingGuard.RequireEmployeeAsync(
            _context, _currentUser, segment.TenantId, segment.BranchId, request.EmployeeId, cancellationToken);
        var segmentType = Validate(request);
        segment.EmployeeId = request.EmployeeId;
        segment.DayOfWeek = request.DayOfWeek;
        segment.SegmentType = segmentType;
        segment.StartTime = request.StartTime;
        segment.EndTime = request.EndTime;
        segment.IsActive = request.IsActive;
        await EnsureNoOverlapAsync(segment, segment.StaffScheduleId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Map(segment);
    }

    public async Task DeleteAsync(
        long staffScheduleId,
        CancellationToken cancellationToken = default)
    {
        var segment = await RequireAsync(staffScheduleId, cancellationToken);
        _context.StaffSchedules.Remove(segment);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static Domain.Enums.ScheduleSegmentType Validate(SaveStaffScheduleRequest request)
    {
        if (request.DayOfWeek > 6)
        {
            throw new SchedulingException("Day of week must be from Sunday (0) through Saturday (6).");
        }

        if (request.EndTime <= request.StartTime)
        {
            throw new SchedulingException("Schedule end time must be after start time.");
        }

        return SchedulingGuard.ParseSegmentType(request.SegmentType);
    }

    private async Task EnsureNoOverlapAsync(
        StaffSchedule segment,
        long? excludedId,
        CancellationToken cancellationToken)
    {
        var overlaps = await _context.StaffSchedules.AnyAsync(
            item =>
                item.EmployeeId == segment.EmployeeId &&
                item.DayOfWeek == segment.DayOfWeek &&
                item.StaffScheduleId != excludedId &&
                item.StartTime < segment.EndTime &&
                item.EndTime > segment.StartTime,
            cancellationToken);

        if (overlaps)
        {
            throw new SchedulingException("Schedule segments for the same employee and day cannot overlap.");
        }
    }

    private async Task<StaffSchedule> RequireAsync(
        long staffScheduleId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.RequireTenantId();
        return await _context.StaffSchedules.RequireOwnedAsync(
            _currentUser,
            item => item.StaffScheduleId == staffScheduleId && item.TenantId == tenantId,
            item => item.BranchId,
            cancellationToken);
    }

    private static StaffScheduleResponse Map(StaffSchedule segment)
    {
        return new StaffScheduleResponse
        {
            StaffScheduleId = segment.StaffScheduleId,
            TenantId = segment.TenantId,
            BranchId = segment.BranchId,
            EmployeeId = segment.EmployeeId,
            DayOfWeek = segment.DayOfWeek,
            SegmentType = segment.SegmentType.ToString(),
            StartTime = segment.StartTime,
            EndTime = segment.EndTime,
            IsActive = segment.IsActive
        };
    }
}
