using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Common;
using SalonBooking.Application.Features.CustomerCatalogue;
using SalonBooking.Application.Features.Scheduling;
using SalonBooking.Application.Interfaces;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Infrastructure.Scheduling;
using SalonBooking.Infrastructure.Security;
using SalonBooking.Infrastructure.Storage;
using SalonBooking.Persistence.Context;

namespace SalonBooking.Infrastructure.Services;

public sealed class CustomerCatalogueService : ICustomerCatalogueService
{
    private readonly SalonBookingDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly CatalogueFileStore _files;

    public CustomerCatalogueService(
        SalonBookingDbContext context,
        ICurrentUserService currentUser,
        CatalogueFileStore files)
    {
        _context = context;
        _currentUser = currentUser;
        _files = files;
    }

    public async Task<IReadOnlyList<CustomerBranchResponse>> ListBranchesAsync(CancellationToken cancellationToken = default)
    {
        var customer = await RequireCustomerAsync(cancellationToken);
        var branches = await VisibleBranches(customer.TenantId).OrderBy(item => item.BranchName).ToListAsync(cancellationToken);
        return branches.Select(MapBranch).ToList();
    }

    public async Task<CustomerBranchResponse> GetBranchAsync(long branchId, CancellationToken cancellationToken = default)
    {
        var customer = await RequireCustomerAsync(cancellationToken);
        var branch = await VisibleBranches(customer.TenantId)
            .FirstOrDefaultAsync(item => item.BranchId == branchId, cancellationToken);
        if (branch == null)
        {
            throw new KeyNotFoundException();
        }

        return MapBranch(branch);
    }

    public async Task<IReadOnlyList<CustomerServiceResponse>> ListServicesAsync(long branchId, CancellationToken cancellationToken = default)
    {
        var customer = await RequireCustomerAsync(cancellationToken);
        await RequireVisibleBranchAsync(customer.TenantId, branchId, cancellationToken);
        var services = await VisibleServices(customer.TenantId, branchId)
            .OrderBy(item => item.ServiceName)
            .ToListAsync(cancellationToken);
        return services.Select(MapService).ToList();
    }

    public async Task<CustomerServiceResponse> GetServiceAsync(long branchId, long serviceId, CancellationToken cancellationToken = default)
    {
        var customer = await RequireCustomerAsync(cancellationToken);
        await RequireVisibleBranchAsync(customer.TenantId, branchId, cancellationToken);
        var service = await VisibleServices(customer.TenantId, branchId)
            .FirstOrDefaultAsync(item => item.ServiceId == serviceId, cancellationToken);
        if (service == null)
        {
            throw new KeyNotFoundException();
        }

        return MapService(service);
    }

    public async Task<IReadOnlyList<CustomerStylistResponse>> ListStylistsAsync(
        long branchId,
        long serviceId,
        CancellationToken cancellationToken = default)
    {
        var customer = await RequireCustomerAsync(cancellationToken);
        await RequireVisibleBranchAsync(customer.TenantId, branchId, cancellationToken);
        var service = await VisibleServices(customer.TenantId, branchId)
            .FirstOrDefaultAsync(item => item.ServiceId == serviceId, cancellationToken);
        if (service == null)
        {
            throw new KeyNotFoundException();
        }

        var employees = await EligibleEmployees(customer.TenantId, branchId, serviceId).ToListAsync(cancellationToken);
        var result = new List<CustomerStylistResponse>();
        foreach (var employee in employees.OrderBy(item => item.FirstName))
        {
            result.Add(await MapStylistAsync(employee, cancellationToken));
        }

        return result;
    }

    public async Task<CustomerStylistResponse> GetStylistAsync(long employeeId, CancellationToken cancellationToken = default)
    {
        var customer = await RequireCustomerAsync(cancellationToken);
        var employee = await _context.Employees
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.EmployeeId == employeeId &&
                    item.TenantId == customer.TenantId &&
                    item.IsActive &&
                    !item.IsDeleted &&
                    item.IsCustomerVisible,
                cancellationToken);
        if (employee == null)
        {
            throw new KeyNotFoundException();
        }

        var eligible = await _context.EmployeeServices
            .IgnoreQueryFilters()
            .AnyAsync(
                item => item.EmployeeId == employee.EmployeeId &&
                    item.TenantId == customer.TenantId &&
                    item.BranchId == employee.BranchId &&
                    item.IsActive &&
                    !item.IsDeleted,
                cancellationToken);
        if (!eligible)
        {
            throw new KeyNotFoundException();
        }

        await RequireVisibleBranchAsync(customer.TenantId, employee.BranchId, cancellationToken);
        return await MapStylistAsync(employee, cancellationToken);
    }

    public async Task<CustomerAvailabilityResponse> GetAvailabilityAsync(
        long branchId,
        long serviceId,
        DateOnly date,
        long? employeeId,
        CancellationToken cancellationToken = default)
    {
        var customer = await RequireCustomerAsync(cancellationToken);
        if (date == default)
        {
            throw new SchedulingException("A date is required.");
        }

        await RequireVisibleBranchAsync(customer.TenantId, branchId, cancellationToken);
        var service = await VisibleServices(customer.TenantId, branchId)
            .FirstOrDefaultAsync(item => item.ServiceId == serviceId, cancellationToken);
        if (service == null)
        {
            throw new KeyNotFoundException();
        }

        var employees = await EligibleEmployees(customer.TenantId, branchId, serviceId).ToListAsync(cancellationToken);
        if (employeeId is > 0)
        {
            employees = employees.Where(item => item.EmployeeId == employeeId).ToList();
            if (employees.Count == 0)
            {
                throw new KeyNotFoundException();
            }
        }

        var merged = new SortedDictionary<DateTime, CustomerSlotResponse>();
        foreach (var employee in employees)
        {
            var shift = await EmployeeAvailabilityLoader.GetWindowsAsync(
                _context,
                customer.TenantId,
                branchId,
                employee.EmployeeId,
                date,
                excludedAppointmentServiceId: null,
                subtractBookings: false,
                cancellationToken);
            var free = await EmployeeAvailabilityLoader.GetFreeWindowsAsync(
                _context,
                customer.TenantId,
                branchId,
                employee.EmployeeId,
                date,
                excludedAppointmentServiceId: null,
                cancellationToken);
            var open = AvailabilityMath.Slots(free, service.DurationMinutes)
                .Select(slot => slot.Start)
                .ToHashSet();
            foreach (var slot in AvailabilityMath.Slots(shift, service.DurationMinutes))
            {
                var available = open.Contains(slot.Start);
                if (!merged.TryGetValue(slot.Start, out var existing))
                {
                    merged[slot.Start] = new CustomerSlotResponse
                    {
                        StartTime = slot.Start,
                        EndTime = slot.End,
                        Available = available
                    };
                }
                else if (available)
                {
                    existing.Available = true;
                }
            }
        }

        var slots = merged.Values
            .Where(slot => !SalonTime.IsPastSlot(date, slot.StartTime))
            .ToList();
        return new CustomerAvailabilityResponse
        {
            BranchId = branchId,
            ServiceId = serviceId,
            EmployeeId = employeeId is > 0 ? employeeId : null,
            DurationMinutes = service.DurationMinutes,
            Slots = slots
        };
    }

    public async Task<StoredFileContent?> OpenBranchPhotoAsync(long branchId, CancellationToken cancellationToken = default)
    {
        var customer = await RequireCustomerAsync(cancellationToken);
        var branch = await VisibleBranches(customer.TenantId)
            .FirstOrDefaultAsync(item => item.BranchId == branchId, cancellationToken);
        return branch == null ? null : _files.Open(CatalogueImageKind.Branches, branch.PhotoPath);
    }

    public async Task<StoredFileContent?> OpenServicePhotoAsync(long serviceId, CancellationToken cancellationToken = default)
    {
        var customer = await RequireCustomerAsync(cancellationToken);
        var service = await _context.Services
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(item => item.ServiceCategory)
            .FirstOrDefaultAsync(
                item => item.ServiceId == serviceId &&
                    item.TenantId == customer.TenantId &&
                    item.IsActive &&
                    !item.IsDeleted &&
                    item.IsCustomerVisible,
                cancellationToken);
        if (service == null || !await VisibleBranches(customer.TenantId).AnyAsync(item => item.BranchId == service.BranchId, cancellationToken))
        {
            return null;
        }

        return _files.Open(CatalogueImageKind.Services, service.PhotoPath);
    }

    public async Task<StoredFileContent?> OpenStylistPhotoAsync(long employeeId, CancellationToken cancellationToken = default)
    {
        try
        {
            var stylist = await GetStylistAsync(employeeId, cancellationToken);
            var employee = await _context.Employees.IgnoreQueryFilters().AsNoTracking()
                .FirstAsync(item => item.EmployeeId == stylist.EmployeeId, cancellationToken);
            return _files.Open(CatalogueImageKind.Employees, employee.PhotoPath);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private IQueryable<Branch> VisibleBranches(long tenantId)
    {
        return _context.Branches
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.IsActive &&
                !item.IsDeleted &&
                item.IsCustomerVisible);
    }

    private IQueryable<Service> VisibleServices(long tenantId, long branchId)
    {
        return _context.Services
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(item => item.ServiceCategory)
            .Where(item =>
                item.TenantId == tenantId &&
                item.BranchId == branchId &&
                item.IsActive &&
                !item.IsDeleted &&
                item.IsCustomerVisible &&
                item.ServiceCategory.IsActive &&
                !item.ServiceCategory.IsDeleted);
    }

    private IQueryable<Employee> EligibleEmployees(long tenantId, long branchId, long serviceId)
    {
        var ids = _context.EmployeeServices
            .IgnoreQueryFilters()
            .Where(item =>
                item.TenantId == tenantId &&
                item.BranchId == branchId &&
                item.ServiceId == serviceId &&
                item.IsActive &&
                !item.IsDeleted)
            .Select(item => item.EmployeeId);

        return _context.Employees
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item =>
                ids.Contains(item.EmployeeId) &&
                item.TenantId == tenantId &&
                item.BranchId == branchId &&
                item.IsActive &&
                !item.IsDeleted &&
                item.IsCustomerVisible);
    }

    private async Task RequireVisibleBranchAsync(long tenantId, long branchId, CancellationToken cancellationToken)
    {
        var exists = await VisibleBranches(tenantId).AnyAsync(item => item.BranchId == branchId, cancellationToken);
        if (!exists)
        {
            throw new KeyNotFoundException();
        }
    }

    private async Task<Customer> RequireCustomerAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.UserCategory != UserCategory.Customer || _currentUser.CustomerId is not > 0)
        {
            throw new UnauthorizedAccessException("A customer sign-in is required.");
        }

        var tenantId = _currentUser.RequireTenantId();
        var customer = await _context.Customers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.CustomerId == _currentUser.CustomerId &&
                    item.TenantId == tenantId &&
                    item.IsActive &&
                    !item.IsDeleted,
                cancellationToken);
        if (customer == null)
        {
            throw new UnauthorizedAccessException("A customer sign-in is required.");
        }

        return customer;
    }

    private async Task<CustomerStylistResponse> MapStylistAsync(Employee employee, CancellationToken cancellationToken)
    {
        var qualifications = await _context.EmployeeQualifications
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(item => item.Qualification)
            .Where(item =>
                item.EmployeeId == employee.EmployeeId &&
                item.TenantId == employee.TenantId &&
                item.IsActive &&
                !item.IsDeleted &&
                item.IsCustomerVisible &&
                item.Qualification.IsActive &&
                !item.Qualification.IsDeleted)
            .OrderBy(item => item.Qualification.QualificationName)
            .ToListAsync(cancellationToken);

        return new CustomerStylistResponse
        {
            EmployeeId = employee.EmployeeId,
            BranchId = employee.BranchId,
            DisplayName = $"{employee.FirstName} {employee.LastName}".Trim(),
            Specialisation = employee.Specialisation,
            ProfessionalDescription = employee.ProfessionalDescription,
            HasPhoto = !string.IsNullOrWhiteSpace(employee.PhotoPath),
            Qualifications = qualifications.Select(item => new CustomerQualificationResponse
            {
                EmployeeQualificationId = item.EmployeeQualificationId,
                QualificationName = item.Qualification.QualificationName,
                QualificationLevel = item.Qualification.QualificationLevel,
                AwardingInstitution = item.AwardingInstitution,
                CompletionYear = item.CompletionYear,
                VerificationStatus = item.VerificationStatus.ToString(),
                IsVerified = item.VerificationStatus == QualificationVerificationStatus.Verified
            }).ToList()
        };
    }

    private static CustomerBranchResponse MapBranch(Branch branch)
    {
        return new CustomerBranchResponse
        {
            BranchId = branch.BranchId,
            BranchName = branch.BranchName,
            Highlight = branch.Highlight,
            AddressLine1 = branch.AddressLine1,
            AddressLine2 = branch.AddressLine2,
            City = branch.City,
            PhoneNo = branch.PhoneNo,
            Email = branch.Email,
            HasPhoto = !string.IsNullOrWhiteSpace(branch.PhotoPath)
        };
    }

    private static CustomerServiceResponse MapService(Service service)
    {
        return new CustomerServiceResponse
        {
            ServiceId = service.ServiceId,
            BranchId = service.BranchId,
            ServiceCategoryId = service.ServiceCategoryId,
            CategoryName = service.ServiceCategory.CategoryName,
            ServiceName = service.ServiceName,
            Description = service.Description,
            DurationMinutes = service.DurationMinutes,
            Price = service.Price,
            IsPopular = service.IsPopular,
            HasPhoto = !string.IsNullOrWhiteSpace(service.PhotoPath)
        };
    }
}
