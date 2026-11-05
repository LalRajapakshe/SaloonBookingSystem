using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Branch.DTOs;
using SalonBooking.Application.Features.Employee.DTOs;
using SalonBooking.Application.Features.Service.DTOs;
using SalonBooking.Application.Interfaces;
using SalonBooking.Infrastructure.Security;
using SalonBooking.Infrastructure.Storage;
using SalonBooking.Persistence.Context;

namespace SalonBooking.Infrastructure.Services;

public sealed class CatalogueImageAdminService
{
    private readonly SalonBookingDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly CatalogueFileStore _files;

    public CatalogueImageAdminService(
        SalonBookingDbContext context,
        ICurrentUserService currentUser,
        CatalogueFileStore files)
    {
        _context = context;
        _currentUser = currentUser;
        _files = files;
    }

    public Task<BranchResponse> SaveBranchPhotoAsync(long branchId, byte[] content, CancellationToken cancellationToken = default)
    {
        return UpdateBranchAsync(branchId, content, remove: false, cancellationToken);
    }

    public Task<BranchResponse> RemoveBranchPhotoAsync(long branchId, CancellationToken cancellationToken = default)
    {
        return UpdateBranchAsync(branchId, [], remove: true, cancellationToken);
    }

    public async Task<StoredFileContent?> OpenBranchPhotoAsync(long branchId, CancellationToken cancellationToken = default)
    {
        var branch = await RequireBranchAsync(branchId, cancellationToken);
        return _files.Open(CatalogueImageKind.Branches, branch.PhotoPath);
    }

    public async Task<ServiceResponse> SaveServicePhotoAsync(long serviceId, byte[] content, CancellationToken cancellationToken = default)
    {
        var service = await RequireServiceAsync(serviceId, cancellationToken);
        var previous = service.PhotoPath;
        var stored = await _files.SaveAsync(CatalogueImageKind.Services, content, cancellationToken);
        service.PhotoPath = stored;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            service.PhotoPath = previous;
            _files.Delete(CatalogueImageKind.Services, stored);
            throw;
        }

        if (!string.Equals(previous, stored, StringComparison.Ordinal))
        {
            _files.Delete(CatalogueImageKind.Services, previous);
        }

        return ServiceResponse.From(service);
    }

    public async Task<ServiceResponse> RemoveServicePhotoAsync(long serviceId, CancellationToken cancellationToken = default)
    {
        var service = await RequireServiceAsync(serviceId, cancellationToken);
        var previous = service.PhotoPath;
        service.PhotoPath = null;
        await _context.SaveChangesAsync(cancellationToken);
        _files.Delete(CatalogueImageKind.Services, previous);
        return ServiceResponse.From(service);
    }

    public async Task<StoredFileContent?> OpenServicePhotoAsync(long serviceId, CancellationToken cancellationToken = default)
    {
        var service = await RequireServiceAsync(serviceId, cancellationToken);
        return _files.Open(CatalogueImageKind.Services, service.PhotoPath);
    }

    public async Task<EmployeeResponse> SaveEmployeePhotoAsync(long employeeId, byte[] content, CancellationToken cancellationToken = default)
    {
        var employee = await RequireEmployeeAsync(employeeId, cancellationToken);
        var previous = employee.PhotoPath;
        var stored = await _files.SaveAsync(CatalogueImageKind.Employees, content, cancellationToken);
        employee.PhotoPath = stored;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            employee.PhotoPath = previous;
            _files.Delete(CatalogueImageKind.Employees, stored);
            throw;
        }

        if (!string.Equals(previous, stored, StringComparison.Ordinal))
        {
            _files.Delete(CatalogueImageKind.Employees, previous);
        }

        return EmployeeResponse.From(employee);
    }

    public async Task<EmployeeResponse> RemoveEmployeePhotoAsync(long employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await RequireEmployeeAsync(employeeId, cancellationToken);
        var previous = employee.PhotoPath;
        employee.PhotoPath = null;
        await _context.SaveChangesAsync(cancellationToken);
        _files.Delete(CatalogueImageKind.Employees, previous);
        return EmployeeResponse.From(employee);
    }

    public async Task<StoredFileContent?> OpenEmployeePhotoAsync(long employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await RequireEmployeeAsync(employeeId, cancellationToken);
        return _files.Open(CatalogueImageKind.Employees, employee.PhotoPath);
    }

    private async Task<BranchResponse> UpdateBranchAsync(
        long branchId,
        byte[] content,
        bool remove,
        CancellationToken cancellationToken)
    {
        var branch = await RequireBranchAsync(branchId, cancellationToken);
        if (remove)
        {
            var previous = branch.PhotoPath;
            branch.PhotoPath = null;
            await _context.SaveChangesAsync(cancellationToken);
            _files.Delete(CatalogueImageKind.Branches, previous);
        }
        else
        {
            var previous = branch.PhotoPath;
            var stored = await _files.SaveAsync(CatalogueImageKind.Branches, content, cancellationToken);
            branch.PhotoPath = stored;
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                branch.PhotoPath = previous;
                _files.Delete(CatalogueImageKind.Branches, stored);
                throw;
            }

            if (!string.Equals(previous, stored, StringComparison.Ordinal))
            {
                _files.Delete(CatalogueImageKind.Branches, previous);
            }
        }

        return new BranchResponse
        {
            TenantId = branch.TenantId,
            BranchId = branch.BranchId,
            BranchCode = branch.BranchCode,
            BranchName = branch.BranchName,
            AddressLine1 = branch.AddressLine1,
            AddressLine2 = branch.AddressLine2,
            City = branch.City,
            PhoneNo = branch.PhoneNo,
            Email = branch.Email,
            ManagerName = branch.ManagerName,
            IsHeadOffice = branch.IsHeadOffice,
            Highlight = branch.Highlight,
            PhotoPath = branch.PhotoPath,
            IsCustomerVisible = branch.IsCustomerVisible,
            IsActive = branch.IsActive
        };
    }

    private async Task<Domain.Entities.Branch> RequireBranchAsync(long branchId, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.RequireTenantId();
        return await _context.Branches.RequireOwnedAsync(
            _currentUser,
            item => item.BranchId == branchId && item.TenantId == tenantId && !item.IsDeleted,
            item => item.BranchId,
            cancellationToken);
    }

    private async Task<Domain.Entities.Service> RequireServiceAsync(long serviceId, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.RequireTenantId();
        return await _context.Services
            .Include(item => item.ServiceCategory)
            .RequireOwnedAsync(
            _currentUser,
            item => item.ServiceId == serviceId && item.TenantId == tenantId && !item.IsDeleted,
            item => item.BranchId,
            cancellationToken);
    }

    private async Task<Domain.Entities.Employee> RequireEmployeeAsync(long employeeId, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.RequireTenantId();
        return await _context.Employees.RequireOwnedAsync(
            _currentUser,
            item => item.EmployeeId == employeeId && item.TenantId == tenantId && !item.IsDeleted,
            item => item.BranchId,
            cancellationToken);
    }
}
