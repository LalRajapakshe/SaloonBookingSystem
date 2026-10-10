using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Scheduling;
using SalonBooking.Application.Interfaces;
using EmployeeCapability = SalonBooking.Domain.Entities.EmployeeService;
using SalonBooking.Infrastructure.Scheduling;
using SalonBooking.Infrastructure.Security;
using SalonBooking.Persistence.Context;

namespace SalonBooking.Infrastructure.Services;

public sealed class EmployeeCapabilityService : IEmployeeCapabilityService
{
    private readonly SalonBookingDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public EmployeeCapabilityService(
        SalonBookingDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<EmployeeCapabilityResponse>> GetAsync(
        EmployeeCapabilityQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var branchId = _currentUser.ResolveBranchId(request.BranchId);
        var query = _context.EmployeeServices
            .Where(item => item.TenantId == tenantId && item.BranchId == branchId);

        if (request.EmployeeId is > 0)
        {
            query = query.Where(item => item.EmployeeId == request.EmployeeId);
        }

        if (request.ServiceId is > 0)
        {
            query = query.Where(item => item.ServiceId == request.ServiceId);
        }

        var items = await query
            .OrderBy(item => item.PreferenceOrder)
            .ThenBy(item => item.EmployeeServiceId)
            .ToListAsync(cancellationToken);

        return items.Select(item => Map(item)).ToList();
    }

    public async Task<EmployeeCapabilityResponse> AssignAsync(
        AssignEmployeeCapabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var branchId = _currentUser.ResolveBranchId(request.BranchId);
        await SchedulingGuard.RequireBranchAsync(_context, tenantId, branchId, cancellationToken);
        await SchedulingGuard.RequireEmployeeAsync(
            _context, _currentUser, tenantId, branchId, request.EmployeeId, cancellationToken);
        await SchedulingGuard.RequireServiceAsync(
            _context, _currentUser, tenantId, branchId, request.ServiceId, cancellationToken);

        var exists = await _context.EmployeeServices.AnyAsync(
            item => item.EmployeeId == request.EmployeeId && item.ServiceId == request.ServiceId,
            cancellationToken);

        if (exists)
        {
            throw new SchedulingException("This employee is already assigned to the service.");
        }

        var capability = new EmployeeCapability
        {
            TenantId = tenantId,
            BranchId = branchId,
            EmployeeId = request.EmployeeId,
            ServiceId = request.ServiceId,
            PreferenceOrder = request.PreferenceOrder
        };
        _context.EmployeeServices.Add(capability);
        await _context.SaveChangesAsync(cancellationToken);
        return Map(capability);
    }

    public async Task<EmployeeCapabilityResponse> UpdateAsync(
        long employeeServiceId,
        UpdateEmployeeCapabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        var capability = await RequireAsync(employeeServiceId, cancellationToken);
        capability.IsActive = request.IsActive;
        capability.PreferenceOrder = request.PreferenceOrder;
        await _context.SaveChangesAsync(cancellationToken);
        return Map(capability);
    }

    public async Task RemoveAsync(
        long employeeServiceId,
        CancellationToken cancellationToken = default)
    {
        var capability = await RequireAsync(employeeServiceId, cancellationToken);
        _context.EmployeeServices.Remove(capability);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<EmployeeCapability> RequireAsync(
        long employeeServiceId,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.RequireTenantId();
        return await _context.EmployeeServices.RequireOwnedAsync(
            _currentUser,
            item => item.EmployeeServiceId == employeeServiceId && item.TenantId == tenantId,
            item => item.BranchId,
            cancellationToken);
    }

    private static EmployeeCapabilityResponse Map(EmployeeCapability capability)
    {
        return new EmployeeCapabilityResponse
        {
            EmployeeServiceId = capability.EmployeeServiceId,
            TenantId = capability.TenantId,
            BranchId = capability.BranchId,
            EmployeeId = capability.EmployeeId,
            ServiceId = capability.ServiceId,
            IsActive = capability.IsActive,
            PreferenceOrder = capability.PreferenceOrder
        };
    }
}
