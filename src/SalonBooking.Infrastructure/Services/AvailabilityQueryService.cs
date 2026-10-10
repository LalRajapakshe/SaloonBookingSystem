using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Features.Scheduling;
using SalonBooking.Application.Interfaces;
using SalonBooking.Infrastructure.Scheduling;
using SalonBooking.Infrastructure.Security;
using SalonBooking.Persistence.Context;

namespace SalonBooking.Infrastructure.Services;

public sealed class AvailabilityQueryService : IAvailabilityQueryService
{
    private readonly SalonBookingDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AvailabilityQueryService(
        SalonBookingDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<List<EmployeeAvailabilityResponse>> GetAvailabilityAsync(
        AvailabilityQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var branchId = _currentUser.ResolveBranchId(request.BranchId);
        if (request.Date == default)
        {
            throw new Application.Common.SchedulingException("A date is required.");
        }

        var service = await SchedulingGuard.RequireServiceAsync(
            _context, _currentUser, tenantId, branchId, request.ServiceId, cancellationToken);

        var capabilities = _context.EmployeeServices
            .Where(item =>
                item.BranchId == branchId &&
                item.ServiceId == service.ServiceId &&
                item.IsActive);

        if (request.EmployeeId is > 0)
        {
            capabilities = capabilities.Where(item => item.EmployeeId == request.EmployeeId);
        }

        var employeeIds = await capabilities
            .Select(item => item.EmployeeId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var employees = await _context.Employees
            .Where(employee => employeeIds.Contains(employee.EmployeeId) && employee.IsActive)
            .Select(employee => new { employee.EmployeeId, employee.FirstName, employee.LastName })
            .ToListAsync(cancellationToken);

        var results = new List<EmployeeAvailabilityResponse>();
        foreach (var employee in employees.OrderBy(employee => employee.FirstName))
        {
            var free = await EmployeeAvailabilityLoader.GetFreeWindowsAsync(
                _context,
                branchId,
                employee.EmployeeId,
                request.Date,
                excludedAppointmentServiceId: null,
                cancellationToken);

            results.Add(new EmployeeAvailabilityResponse
            {
                EmployeeId = employee.EmployeeId,
                EmployeeName = $"{employee.FirstName} {employee.LastName}".Trim(),
                ServiceId = service.ServiceId,
                DurationMinutes = service.DurationMinutes,
                Slots = AvailabilityMath.Slots(free, service.DurationMinutes)
                    .Select(slot => new AvailabilitySlotResponse
                    {
                        StartTime = slot.Start,
                        EndTime = slot.End
                    })
                    .ToList()
            });
        }

        return results;
    }
}
