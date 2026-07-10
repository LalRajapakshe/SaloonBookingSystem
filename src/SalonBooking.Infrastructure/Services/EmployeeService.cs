using SalonBooking.Application.Features.Employee.DTOs;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Common;
using SalonBooking.Persistence.Context;
using SalonBooking.Domain.Entities;
using SalonBooking.Infrastructure.Authentication;
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
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t =>
                t.TenantId == _currentUserService.TenantId);

        if (tenant == null)
            throw new Exception("Tenant not found.");  

        var branch = await _context.Branches
        .FirstOrDefaultAsync(b =>
        b.BranchId == _currentUserService.BranchId &&
        b.TenantId == _currentUserService.TenantId &&
        b.IsActive); //&& !b.IsDeleted

        if (branch == null)

            throw new Exception("Branch not found.");      

        var exists = await _context.Employees.AnyAsync(c =>
            c.TenantId == _currentUserService.TenantId &&
            c.MobileNo == request.MobileNo);  //&& !c.IsDeleted   
        if (exists){     
             throw new Exception("Employee(Mobile No) already exists.."); 
        }
        var lastEmployee = await _context.Employees
           .OrderByDescending(e => e.EmployeeId).FirstOrDefaultAsync();
                long  nextNumber = lastEmployee == null
                    ? 1
                    : lastEmployee.EmployeeId + 1;
        var employee = new Employee
            {
                TenantId = _currentUserService.TenantId, // Temporary until multi-tenant login is implemented
                BranchId = _currentUserService.BranchId, // Temporary until multi-tenant login is implemented

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

        return new EmployeeResponse
        {
            EmployeeId = employee.EmployeeId,
            EmployeeCode = employee.EmployeeCode,
            FullName = $"{employee.FirstName} {employee.LastName}",
            MobileNo = employee.MobileNo,
            Email = employee.Email,
            Gender = employee.Gender,
            Designation = employee.Designation,
            TenantId = employee.TenantId,
            BranchId = employee.BranchId
        };
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

    var items = employees.Select(e => new EmployeeResponse
    {
    EmployeeId = e.EmployeeId,
    EmployeeCode = e.EmployeeCode,
    FullName = e.FirstName + " " + e.LastName,
    MobileNo = e.MobileNo,
    Email = e.Email,
    Gender = e.Gender,
    Designation = e.Designation,
    TenantId = e.TenantId,
    BranchId = e.BranchId
    }).ToList();

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
        return await _context.Employees
            .Where(e => e.TenantId == _currentUserService.TenantId && e.IsActive) //&& !e.IsDeleted
            .OrderBy(e => e.EmployeeCode)
            .Select(e => new EmployeeResponse
            {
                EmployeeId = e.EmployeeId,
                EmployeeCode = e.EmployeeCode,
                FullName = e.FirstName + " " + e.LastName,
                MobileNo = e.MobileNo,
                Email = e.Email,
                Gender = e.Gender,
                Designation = e.Designation,
                TenantId = e.TenantId,
                BranchId = e.BranchId
            })
            .ToListAsync();
    }

public async Task<EmployeeResponse?> GetByIdAsync(long employeeId)
{
    return await _context.Employees
        .Where(e => e.TenantId == _currentUserService.TenantId && e.EmployeeId == employeeId && e.IsActive) //&& !e.IsDeleted
        .Select(e => new    EmployeeResponse
        {
            EmployeeId = e.EmployeeId,
            EmployeeCode = e.EmployeeCode,
            FullName = e.FirstName + " " + e.LastName,
            MobileNo = e.MobileNo,
            Email = e.Email,
            Gender = e.Gender,
            Designation = e.Designation,
            TenantId = e.TenantId,
            BranchId = e.BranchId
        })
        .FirstOrDefaultAsync();
}

public async Task<EmployeeResponse> UpdateAsync(
  
    long employeeId,
       UpdateEmployeeRequest  request)
{
    var employee = await _context.Employees
        .FirstOrDefaultAsync(e => e.EmployeeId == employeeId);

    if (employee == null)
        throw new Exception("Employee not found.");

           var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t =>
                t.TenantId == _currentUserService.TenantId);  //&& !t.IsDeleted

        if (tenant == null)
            throw new Exception("Tenant not found.");  

        var branch = await _context.Branches
        .FirstOrDefaultAsync(b =>
        b.BranchId == _currentUserService.BranchId &&
        b.TenantId == _currentUserService.TenantId);  // && !b.IsDeleted

        if (branch == null)
            throw new Exception("Branch not found.");      

        branch = await _context.Branches
            .FirstOrDefaultAsync(b =>
            b.BranchId == request.BranchId &&
            b.TenantId == _currentUserService.TenantId &&
            b.IsActive);  // && !b.IsDeleted

            if (branch == null)
                throw new Exception("Invalid branch.");    

        var exists = await _context.Customers.AnyAsync(c =>
            c.TenantId == _currentUserService.TenantId &&
            c.MobileNo == request.MobileNo &&
            c.CustomerId != employeeId);  // && !c.IsDeleted    
        if (exists){    
             throw new Exception("Customer(Mobile No) already exists.."); 
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
    employee.BranchId = request.BranchId; 

    await _context.SaveChangesAsync();

    return new EmployeeResponse
    {
        EmployeeId = employee.EmployeeId,
        EmployeeCode = employee.EmployeeCode,
        FullName = employee.FirstName + " " + employee.LastName,
        MobileNo = employee.MobileNo,
        Email = employee.Email,
        TenantId = employee.TenantId,
        BranchId = employee.BranchId
    };
}

public async Task DeleteAsync(long employeeId)
    {
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId && e.TenantId == _currentUserService.TenantId);

        if (employee == null)
            throw new Exception("Employee not found.");

        employee.IsActive = false;
        employee.IsDeleted = true;

        await _context.SaveChangesAsync();
    }
}