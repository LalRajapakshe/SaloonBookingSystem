using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Common;
using SalonBooking.Application.Interfaces;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Infrastructure.Security;
using SalonBooking.Persistence.Context;

namespace SalonBooking.Infrastructure.Scheduling;

internal static class SchedulingGuard
{
    public static async Task<Customer> RequireCustomerAsync(
        SalonBookingDbContext context,
        ICurrentUserService user,
        long tenantId,
        long branchId,
        long customerId,
        CancellationToken cancellationToken)
    {
        var customer = await context.Customers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                item => item.CustomerId == customerId && item.TenantId == tenantId && !item.IsDeleted,
                cancellationToken);

        if (customer == null || customer.BranchId != branchId)
        {
            throw new KeyNotFoundException();
        }

        user.EnsureCanAccessBranch(customer.BranchId);
        return customer;
    }

    public static async Task<Employee> RequireEmployeeAsync(
        SalonBookingDbContext context,
        ICurrentUserService user,
        long tenantId,
        long branchId,
        long employeeId,
        CancellationToken cancellationToken)
    {
        var employee = await context.Employees
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                item => item.EmployeeId == employeeId && item.TenantId == tenantId && !item.IsDeleted,
                cancellationToken);

        if (employee == null || employee.BranchId != branchId || !employee.IsActive)
        {
            throw new KeyNotFoundException();
        }

        user.EnsureCanAccessBranch(employee.BranchId);
        return employee;
    }

    public static async Task<Service> RequireServiceAsync(
        SalonBookingDbContext context,
        ICurrentUserService user,
        long tenantId,
        long branchId,
        long serviceId,
        CancellationToken cancellationToken)
    {
        var service = await context.Services
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                item => item.ServiceId == serviceId && item.TenantId == tenantId && !item.IsDeleted,
                cancellationToken);

        if (service == null || service.BranchId != branchId || !service.IsActive)
        {
            throw new KeyNotFoundException();
        }

        user.EnsureCanAccessBranch(service.BranchId);
        return service;
    }

    public static async Task RequireBranchAsync(
        SalonBookingDbContext context,
        long tenantId,
        long branchId,
        CancellationToken cancellationToken)
    {
        var exists = await context.Branches
            .IgnoreQueryFilters()
            .AnyAsync(
                branch => branch.BranchId == branchId &&
                    branch.TenantId == tenantId &&
                    branch.IsActive &&
                    !branch.IsDeleted,
                cancellationToken);

        if (!exists)
        {
            throw new KeyNotFoundException();
        }
    }

    public static AppointmentStatus ParseStatus(string? value, AppointmentStatus fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (!Enum.TryParse<AppointmentStatus>(value, ignoreCase: true, out var status) ||
            !Enum.IsDefined(status))
        {
            throw new SchedulingException("Appointment status is not valid.");
        }

        return status;
    }

    public static ScheduleSegmentType ParseSegmentType(string value)
    {
        if (!Enum.TryParse<ScheduleSegmentType>(value, ignoreCase: true, out var segmentType) ||
            !Enum.IsDefined(segmentType))
        {
            throw new SchedulingException("Schedule segment type is not valid.");
        }

        return segmentType;
    }

    public static LeaveType ParseLeaveType(string value)
    {
        if (!Enum.TryParse<LeaveType>(value, ignoreCase: true, out var leaveType) ||
            !Enum.IsDefined(leaveType))
        {
            throw new SchedulingException("Leave type is not valid.");
        }

        return leaveType;
    }

    public static LeaveStatus ParseLeaveStatus(string value)
    {
        if (!Enum.TryParse<LeaveStatus>(value, ignoreCase: true, out var status) ||
            !Enum.IsDefined(status))
        {
            throw new SchedulingException("Leave status is not valid.");
        }

        return status;
    }

    public static byte[] ReadRowVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new SchedulingException("Row version is required.");
        }

        try
        {
            var bytes = Convert.FromBase64String(value);
            if (bytes.Length == 0)
            {
                throw new SchedulingException("Row version is required.");
            }

            return bytes;
        }
        catch (FormatException)
        {
            throw new SchedulingException("Row version is invalid.");
        }
    }

    public static string WriteRowVersion(byte[] value)
    {
        return Convert.ToBase64String(value);
    }
}
