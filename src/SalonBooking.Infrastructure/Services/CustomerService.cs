using SalonBooking.Application.Features.Customer.DTOs;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Common;
using SalonBooking.Persistence.Context;
using SalonBooking.Domain.Entities;
using SalonBooking.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace SalonBooking.Infrastructure.Services;

public class CustomerService : ICustomerService
{
    private readonly SalonBookingDbContext _context;
     private readonly ICurrentUserService _currentUserService;

    public CustomerService(SalonBookingDbContext context, ICurrentUserService currentUserService)
    {
            _context = context;
            _currentUserService = currentUserService;
    }
    public async Task<CustomerResponse> CreateAsync(CreateCustomerRequest request)
    {
        var tenantId = _currentUserService.RequireTenantId();
        var branchId = _currentUserService.ResolveBranchId(request.BranchId);

        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t =>
                t.TenantId == tenantId);
                // && !t.IsDeleted
        if (tenant == null)
            throw new Exception("Tenant not found.");  

        var branch = await _context.Branches
        .FirstOrDefaultAsync(b =>
        b.BranchId == branchId &&
        b.TenantId == tenantId);
        //&& !b.IsDeleted
        if (branch == null)
            throw new Exception("Branch not found.");      

        var exists = await _context.Customers.AnyAsync(c =>
            c.TenantId == tenantId &&
            c.BranchId == branchId &&
            c.MobileNo == request.MobileNo );   //&& !c.IsDeleted
        if (exists){     
             throw new Exception("Customer(Mobile No) already exists.."); 
        }
        var lastCustomer = await _context.Customers
           .IgnoreQueryFilters()
           .Where(e => e.TenantId == tenantId)
           .OrderByDescending(c => c.CustomerId).FirstOrDefaultAsync();
           
                long  nextNumber = lastCustomer == null
                    ? 1
                    : lastCustomer.CustomerId + 1;
        var customer = new Customer
            {
                TenantId = tenantId, 
                BranchId = branchId, 

                CustomerCode = $"CUS{nextNumber:D6}", 
               // CustomerCode = $"CUS{DateTime.Now.Ticks}",
                FirstName = request.FirstName,
                LastName = request.LastName,
                MobileNo = request.MobileNo,
                Email = request.Email,
                Gender = request.Gender,
                DateOfBirth = request.DateOfBirth,
                Remarks = request.Remarks,
              //  IsActive = true
                //  reatedDate = DateTime.UtcNow
            };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        return CustomerResponse.From(customer);
    }

  public async Task<PagedResult<CustomerResponse>> GetCustomersAsync(CustomerQueryRequest request)
  {
    var query = _context.Customers.Where(c => c.IsActive &&
    c.TenantId == _currentUserService.TenantId &&
        !c.IsDeleted).AsQueryable();
    if (!string.IsNullOrWhiteSpace(request.Search))
    {
    query = query.Where(c =>
        c.FirstName.Contains(request.Search) ||
        c.LastName.Contains(request.Search) ||
        c.MobileNo.Contains(request.Search) ||
        c.Email.Contains(request.Search) ||
        c.CustomerCode.Contains(request.Search));
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

    "customercode" => request.SortOrder == "desc"
        ? query.OrderByDescending(c => c.CustomerCode)
        : query.OrderBy(c => c.CustomerCode),

    _ => query.OrderByDescending(c => c.CustomerId)
   };

   var totalRecords = await query.CountAsync();

   var customers = await query
    .Skip((request.Page - 1) * request.PageSize)
    .Take(request.PageSize)
    .ToListAsync();

    var items = customers.Select(CustomerResponse.From).ToList();

    return new PagedResult<CustomerResponse>
    {
    Page = request.Page,
    PageSize = request.PageSize,
    TotalRecords = totalRecords,
    TotalPages = (int)Math.Ceiling((double)totalRecords / request.PageSize),
    Items = items
    };
  }

  public async Task<List<CustomerResponse>> GetAllAsync()
    {
        var customers = await _context.Customers
            .Where(c => c.TenantId == _currentUserService.TenantId && c.IsActive ) //&& !c.IsDeleted
            .OrderBy(c => c.CustomerCode)
            .ToListAsync();

        return customers.Select(CustomerResponse.From).ToList();
    }

public async Task<CustomerResponse?> GetByIdAsync(long customerId)
{
    var customer = await _context.Customers
        .FirstOrDefaultAsync(c =>
            c.TenantId == _currentUserService.TenantId &&
            c.CustomerId == customerId);

    return customer == null ? null : CustomerResponse.From(customer);
}

public async Task<CustomerResponse> UpdateAsync(
  
    long customerId,
    UpdateCustomerRequest request)
{
    var tenantId = _currentUserService.RequireTenantId();
    var branchId = _currentUserService.ResolveBranchId(request.BranchId);

    var customer = await _context.Customers
        .FirstOrDefaultAsync(c =>
            c.CustomerId == customerId &&
            c.TenantId == tenantId);

    if (customer == null)
        throw new Exception("Customer not found.");

           var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t =>
                t.TenantId == tenantId); // &&
                //!t.IsDeleted);

        if (tenant == null)
            throw new Exception("Tenant not found.");  

        var branch = await _context.Branches
            .FirstOrDefaultAsync(b =>
            b.BranchId == branchId &&
            b.TenantId == tenantId &&
            b.IsActive); // &&
         //   !b.IsDeleted);

            if (branch == null)
                throw new Exception("Invalid branch.");    

        var exists = await _context.Customers.AnyAsync(c =>
            c.TenantId == _currentUserService.TenantId &&
            c.MobileNo == request.MobileNo &&
            c.CustomerId != customerId); // &&
           // !c.IsDeleted);    
        if (exists){    
             throw new Exception("Customer(Mobile No) already exists.."); 
        }

    customer.FirstName = request.FirstName;
    customer.LastName = request.LastName;
    customer.MobileNo = request.MobileNo;
    customer.Email = request.Email;
    customer.Gender = request.Gender;
    customer.DateOfBirth = request.DateOfBirth;
    customer.Remarks = request.Remarks;
    customer.IsActive = request.IsActive;
    //customer.TenantId = request.TenantId;
    customer.BranchId = branchId; 

    await _context.SaveChangesAsync();

    return CustomerResponse.From(customer);
}

public async Task DeleteAsync(long customerId)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId  &&
    c.TenantId == _currentUserService.TenantId);

        if (customer == null)
            throw new Exception("Customer not found.");

        customer.IsActive = false;
        customer.IsDeleted = true;

        await _context.SaveChangesAsync();
    }
}