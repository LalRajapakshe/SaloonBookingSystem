using SalonBooking.Application.Features.Customer.DTOs;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Common;
using SalonBooking.Persistence.Context;
using SalonBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace SalonBooking.Infrastructure.Services;

public class CustomerService : ICustomerService
{
    private readonly SalonBookingDbContext _context;

    public CustomerService(SalonBookingDbContext context)
    {
            _context = context;
    }
    public async Task<CustomerResponse> CreateAsync(CreateCustomerRequest request)
    {
        var lastCustomer = await _context.Customers
           .OrderByDescending(c => c.CustomerId).FirstOrDefaultAsync();
                long  nextNumber = lastCustomer == null
                    ? 1
                    : lastCustomer.CustomerId + 1;
        var customer = new Customer
            {
                TenantId = 1, // Temporary until multi-tenant login is implemented


                CustomerCode = $"CUS{nextNumber:D6}", 
               // CustomerCode = $"CUS{DateTime.Now.Ticks}",
                FirstName = request.FirstName,
                LastName = request.LastName,
                MobileNo = request.MobileNo,
                Email = request.Email,
                Gender = request.Gender,
                DateOfBirth = request.DateOfBirth,
                Remarks = request.Remarks,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        return new CustomerResponse
        {
            CustomerId = customer.CustomerId,
            CustomerCode = customer.CustomerCode,
            FullName = $"{customer.FirstName} {customer.LastName}",
            MobileNo = customer.MobileNo,
            Email = customer.Email
        };
    }

  public async Task<PagedResult<CustomerResponse>> GetCustomersAsync(CustomerQueryRequest request)
  {
    var query = _context.Customers.Where(c => c.IsActive).AsQueryable();
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

    var items = customers.Select(c => new CustomerResponse
    {
    CustomerId = c.CustomerId,
    CustomerCode = c.CustomerCode,
    FullName = c.FirstName + " " + c.LastName,
    MobileNo = c.MobileNo,
    Email = c.Email
    }).ToList();

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
        return await _context.Customers
            .Where(c => c.IsActive)
            .OrderBy(c => c.CustomerCode)
            .Select(c => new CustomerResponse
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                FullName = c.FirstName + " " + c.LastName,
                MobileNo = c.MobileNo,
                Email = c.Email
            })
            .ToListAsync();
    }

public async Task<CustomerResponse?> GetByIdAsync(long customerId)
{
    return await _context.Customers
        .Where(c => c.CustomerId == customerId)
        .Select(c => new CustomerResponse
        {
            CustomerId = c.CustomerId,
            CustomerCode = c.CustomerCode,
            FullName = c.FirstName + " " + c.LastName,
            MobileNo = c.MobileNo,
            Email = c.Email
        })
        .FirstOrDefaultAsync();
}

public async Task<CustomerResponse> UpdateAsync(
    long customerId,
    UpdateCustomerRequest request)
{
    var customer = await _context.Customers
        .FirstOrDefaultAsync(c => c.CustomerId == customerId);

    if (customer == null)
        throw new Exception("Customer not found.");

    customer.FirstName = request.FirstName;
    customer.LastName = request.LastName;
    customer.MobileNo = request.MobileNo;
    customer.Email = request.Email;
    customer.Gender = request.Gender;
    customer.DateOfBirth = request.DateOfBirth;
    customer.Remarks = request.Remarks;

    await _context.SaveChangesAsync();

    return new CustomerResponse
    {
        CustomerId = customer.CustomerId,
        CustomerCode = customer.CustomerCode,
        FullName = customer.FirstName + " " + customer.LastName,
        MobileNo = customer.MobileNo,
        Email = customer.Email
    };
}

public async Task DeleteAsync(long customerId)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer == null)
            throw new Exception("Customer not found.");

        customer.IsActive = false;

        await _context.SaveChangesAsync();
    }
}