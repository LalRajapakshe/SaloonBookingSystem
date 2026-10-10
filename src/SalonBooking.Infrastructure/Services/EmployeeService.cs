using SalonBooking.Application.Features.Employee.DTOs;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Common;
using SalonBooking.Persistence.Context;
using SalonBooking.Domain.Entities;
using SalonBooking.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace SalonBooking.Infrastructure.Services;

public class EmployeeService : IEmployeeService
{
    private readonly SalonBookingDbContext _context;
     private readonly ICurrentUserService _currentUserService;

    public EmployeeService(SalonBookingDbContext context, ICurrentUserService currentUserService)
    {
            _context = context;
            _currentUserService = currentUserService;
    }
    public async Task<EmployeeResponse> CreateAsync(CreateEmployeeRequest request)
    {
        var tenantId = _currentUserService.RequireTenantId();
        var branchId = _currentUserService.ResolveBranchId(request.BranchId);

        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t =>
                t.TenantId == tenantId);

        if (tenant == null)
            throw new KeyNotFoundException();

        var branch = await _context.Branches
        .FirstOrDefaultAsync(b =>
        b.BranchId == branchId &&
        b.TenantId == tenantId &&
        b.IsActive); //&& !b.IsDeleted

        if (branch == null)

            throw new KeyNotFoundException();      

        if (await MobileExistsAsync(tenantId, request.MobileNo))
        {
             throw new Exception("Employee mobile number already exists.");
        }
        var lastEmployee = await _context.Employees
           .IgnoreQueryFilters()
           .Where(e => e.TenantId == tenantId)
           .OrderByDescending(e => e.EmployeeId).FirstOrDefaultAsync();
                long  nextNumber = lastEmployee == null
                    ? 1
                    : lastEmployee.EmployeeId + 1;
        var employee = new Employee
            {
                TenantId = tenantId,
                BranchId = branchId,

                EmployeeCode = $"EMP{nextNumber:D6}", 
                FirstName = request.FirstName,
                LastName = request.LastName,
                MobileNo = request.MobileNo,
                Email = request.Email,
                Gender = request.Gender,
                DateOfBirth = request.DateOfBirth,
                Address = request.Address,
                HireDate = request.HireDate,
                Designation = request.Designation,
                Salary = request.Salary,    
               // IsActive = true
              //  CreatedDate = DateTime.UtcNow
            };

        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();

        return EmployeeResponse.From(employee);
    }

  public async Task<PagedResult<EmployeeResponse>> GetEmployeesAsync(EmployeeQueryRequest request)
  {
    var query = _context.Employees.Where(e => e.IsActive &&
    e.TenantId == _currentUserService.TenantId 
        ).AsQueryable(); //&& !e.IsDeleted
    if (!string.IsNullOrWhiteSpace(request.Search))
    {
    query = query.Where(c =>
        c.FirstName.Contains(request.Search) ||
        c.LastName.Contains(request.Search) ||
        c.MobileNo.Contains(request.Search) ||
        c.Email.Contains(request.Search) ||
        c.EmployeeCode.Contains(request.Search) ||
        c.Designation.Contains(request.Search));
    }
    if (!string.IsNullOrWhiteSpace(request.Gender))
    {
    query = query.Where(c => c.Gender == request.Gender);
    }

    query = request.SortBy.ToLower() switch
    {
    "firstname" => request.SortOrder == "desc"
        ? query.OrderByDescending(c => c.FirstName)
        : query.OrderBy(c => c.FirstName),

    "lastname" => request.SortOrder == "desc"
        ? query.OrderByDescending(c => c.LastName)
        : query.OrderBy(c => c.LastName),

    "EmployeeCode" => request.SortOrder == "desc"
        ? query.OrderByDescending(c => c.EmployeeCode)
        : query.OrderBy(c => c.EmployeeCode),

    _ => query.OrderByDescending(c => c.EmployeeId)
   };

   var totalRecords = await query.CountAsync();

   var employees = await query
    .Skip((request.Page - 1) * request.PageSize)
    .Take(request.PageSize)
    .ToListAsync();

    var items = employees.Select(EmployeeResponse.From).ToList();

    return new PagedResult<EmployeeResponse>
    {
    Page = request.Page,
    PageSize = request.PageSize,
    TotalRecords = totalRecords,
    TotalPages = (int)Math.Ceiling((double)totalRecords / request.PageSize),
    Items = items
    };
  }

  public async Task<List<EmployeeResponse>> GetAllAsync()
    {
        var employees = await _context.Employees
            .Where(e => e.TenantId == _currentUserService.TenantId && e.IsActive) //&& !e.IsDeleted
            .OrderBy(e => e.EmployeeCode)
            .ToListAsync();

        return employees.Select(EmployeeResponse.From).ToList();
    }

public async Task<EmployeeResponse?> GetByIdAsync(long employeeId)
{
    var tenantId = _currentUserService.RequireTenantId();
    var employee = await _context.Employees.RequireOwnedAsync(
        _currentUserService,
        item => item.EmployeeId == employeeId &&
                item.TenantId == tenantId &&
                !item.IsDeleted,
        item => item.BranchId);

    if (!employee.IsActive)
    {
        return null;
    }

    return EmployeeResponse.From(employee);
}

public async Task<EmployeeResponse> UpdateAsync(
  
    long employeeId,
       UpdateEmployeeRequest  request)
{
    var tenantId = _currentUserService.RequireTenantId();
    var employee = await _context.Employees.RequireOwnedAsync(
        _currentUserService,
        item => item.EmployeeId == employeeId &&
                item.TenantId == tenantId &&
                !item.IsDeleted,
        item => item.BranchId);
    var branchId = _currentUserService.ResolveBranchId(request.BranchId);

           var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t =>
                t.TenantId == tenantId);  //&& !t.IsDeleted

        if (tenant == null)
            throw new KeyNotFoundException();

        var branch = await _context.Branches
            .FirstOrDefaultAsync(b =>
            b.BranchId == branchId &&
            b.TenantId == tenantId &&
            b.IsActive);  // && !b.IsDeleted

            if (branch == null)
                throw new KeyNotFoundException();    

        if (await MobileExistsAsync(tenantId, request.MobileNo, employeeId))
        {
             throw new Exception("Employee mobile number already exists."); 
        }

    employee.FirstName = request.FirstName;
    employee.LastName = request.LastName;
    employee.MobileNo = request.MobileNo;
    employee.Email = request.Email;
    employee.Gender = request.Gender;
    employee.DateOfBirth = request.DateOfBirth;
    employee.Address = request.Address;
    employee.HireDate = request.HireDate;
    employee.Designation = request.Designation;
    employee.Salary = request.Salary;
    employee.IsActive = request.IsActive;
    //employee.Remarks = request.Remarks;
    employee.IsActive = request.IsActive;
    //employee.TenantId = request.TenantId;
    employee.BranchId = branchId; 

    await _context.SaveChangesAsync();

    return EmployeeResponse.From(employee);
}

private Task<bool> MobileExistsAsync(
    long tenantId,
    string mobileNo,
    long? exceptEmployeeId = null)
{
    var query = _context.Employees
        .IgnoreQueryFilters()
        .Where(employee =>
            employee.TenantId == tenantId &&
            !employee.IsDeleted &&
            employee.MobileNo == mobileNo);

    if (exceptEmployeeId.HasValue)
    {
        query = query.Where(employee =>
            employee.EmployeeId != exceptEmployeeId.Value);
    }

    return query.AnyAsync();
}

public async Task DeleteAsync(long employeeId)
    {
        var tenantId = _currentUserService.RequireTenantId();
        var employee = await _context.Employees.RequireOwnedAsync(
            _currentUserService,
            item => item.EmployeeId == employeeId &&
                    item.TenantId == tenantId &&
                    !item.IsDeleted,
            item => item.BranchId);

        employee.IsActive = false;
        employee.IsDeleted = true;

        await _context.SaveChangesAsync();
    }
}