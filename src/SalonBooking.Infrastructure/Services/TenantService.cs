using SalonBooking.Application.Features.Tenant.DTOs;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Common;
using SalonBooking.Persistence.Context;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using Microsoft.EntityFrameworkCore;


namespace SalonBooking.Infrastructure.Services;

public class TenantService : ItenantService
{
    private readonly SalonBookingDbContext _context;

    public TenantService(SalonBookingDbContext context)
    {
            _context = context;
    }
    public async Task<TenantResponse> CreateAsync(CreateTenantRequest request)
    {
        var lastTenant = await _context.Tenants
        .OrderByDescending(t => t.TenantId)
        .FirstOrDefaultAsync();

        long nextNumber = lastTenant == null
            ? 1
            : lastTenant.TenantId + 1;

        string tenantCode = $"TEN{nextNumber:D6}";
        var tenant = new Tenant
        {
            TenantCode = tenantCode,
            TenantName = request.TenantName,
            BusinessName = request.BusinessName,
            BusinessRegistrationNo = request.BusinessRegistrationNo,
            ContactPerson = request.ContactPerson,
            PhoneNo = request.PhoneNo,
            Email = request.Email,
            Address = request.Address,
            LogoUrl = request.LogoUrl,

            SubscriptionPlan = SubscriptionPlan.Trial,
            SubscriptionStartDate = DateTime.UtcNow,
            SubscriptionEndDate = DateTime.UtcNow.AddDays(30),

            MaxBranches = request.MaxBranches,
            MaxUsers = request.MaxUsers,

            IsActive = true,
            IsDeleted = false
        };
        _context.Tenants.Add(tenant);

        await _context.SaveChangesAsync();
        return new TenantResponse
        {
            TenantId = tenant.TenantId,
            TenantCode = tenant.TenantCode,
            TenantName = tenant.TenantName,
            BusinessName = tenant.BusinessName,
            ContactPerson = tenant.ContactPerson,
            PhoneNo = tenant.PhoneNo,
            Email = tenant.Email,
            IsActive = tenant.IsActive,
            MaxBranches = tenant.MaxBranches,
            MaxUsers = tenant.MaxUsers
        };
    }

  public async Task<PagedResult<TenantResponse>> GetTenantsAsync(TenantQueryRequest request)
   {
    var query = _context.Tenants.Where(c => c.IsActive).AsQueryable();
    if (!string.IsNullOrWhiteSpace(request.Search))
    {
    query = query.Where(c =>
        c.TenantCode.Contains(request.Search) ||
        c.TenantName.Contains(request.Search) ||
        c.BusinessName.Contains(request.Search) ||
        c.ContactPerson.Contains(request.Search) ||
        c.PhoneNo.Contains(request.Search) ||
        c.Email.Contains(request.Search));
    }
   // if (!string.IsNullOrWhiteSpace(request.Gender))
   // {
   // query = query.Where(c => c.Gender == request.Gender);
   // }

    query = request.SortBy.ToLower() switch
    {
    "TenantCode" => request.SortOrder == "desc"
        ? query.OrderByDescending(c => c.TenantCode)
        : query.OrderBy(c => c.TenantCode),

    "TenantName" => request.SortOrder == "desc"
        ? query.OrderByDescending(c => c.TenantName)
        : query.OrderBy(c => c.TenantName),

    "BusinessName" => request.SortOrder == "desc"
        ? query.OrderByDescending(c => c.BusinessName)
        : query.OrderBy(c => c.BusinessName),

    _ => query.OrderByDescending(c => c.TenantId)
   };

   var totalRecords = await query.CountAsync();

   var tenants = await query
    .Skip((request.Page - 1) * request.PageSize)
    .Take(request.PageSize)
    .ToListAsync();

    var items = tenants.Select(c => new TenantResponse
    {
    TenantId = c.TenantId,
    TenantCode = c.TenantCode,
    TenantName = c.TenantName,
    BusinessName = c.BusinessName,
    ContactPerson = c.ContactPerson,
    PhoneNo  = c.PhoneNo,
    Email = c.Email,
    IsActive = c.IsActive,
    MaxBranches = c.MaxBranches,
    MaxUsers = c.MaxUsers
    }).ToList();
    return new PagedResult<TenantResponse>
    {
    Page = request.Page,
    PageSize = request.PageSize,
    TotalRecords = totalRecords,
    TotalPages = (int)Math.Ceiling((double)totalRecords / request.PageSize),
    Items = items
    };
    }

    public async Task<TenantResponse?> GetByIdAsync(long id)
    {

        return await _context.Tenants
        .Where(c => c.TenantId == id)
        .Select(c => new TenantResponse
        {
            TenantId = c.TenantId,
            TenantCode = c.TenantCode,
            TenantName = c.TenantName,
            BusinessName = c.BusinessName,
            ContactPerson = c.ContactPerson,
            PhoneNo  = c.PhoneNo,
            Email = c.Email,
            IsActive = c.IsActive,
            MaxBranches = c.MaxBranches,
            MaxUsers = c.MaxUsers
        })
        .FirstOrDefaultAsync();
    }

   public async Task<TenantResponse> GetAllAsync(int page, int pageSize)
    {
        return await _context.Tenants
        .Where(c => c.IsActive)
        .OrderBy(c => c.TenantCode)
        .Select(c => new TenantResponse
        {
            TenantId = c.TenantId,
            TenantCode = c.TenantCode,
            TenantName = c.TenantName,
            BusinessName = c.BusinessName,
            ContactPerson = c.ContactPerson,
            PhoneNo  = c.PhoneNo,
            Email = c.Email,
            IsActive = c.IsActive,
            MaxBranches = c.MaxBranches,
            MaxUsers = c.MaxUsers
        })
        .FirstOrDefaultAsync();
    }

   public async Task<TenantResponse?> UpdateAsync(long id, UpdateTenantRequest request)
   {

        var tenant = await _context.Tenants
        .FirstOrDefaultAsync(c => c.TenantId == id);

        if (tenant == null)
            throw new Exception("Tenant not found.");

        tenant.TenantId =id; //request.TenantId; 
       // tenant.TenantCode = request.TenantCode;
        tenant.TenantName = request.TenantName;
        tenant.BusinessName = request.BusinessName;
        tenant.ContactPerson = request.ContactPerson;
        tenant.PhoneNo  = request.PhoneNo;
        tenant.Email = request.Email;
        tenant.IsActive = request.IsActive;
        tenant.MaxBranches = request.MaxBranches;
        tenant.MaxUsers = request.MaxUsers;
        await _context.SaveChangesAsync();

            return new TenantResponse
        {
        TenantId = tenant.TenantId,
        TenantCode = tenant.TenantCode,
        TenantName = tenant.TenantName,
        BusinessName = tenant.BusinessName,
        ContactPerson = tenant.ContactPerson,
        PhoneNo  = tenant.PhoneNo,
        Email = tenant.Email,
        IsActive = tenant.IsActive,
        MaxBranches = tenant.MaxBranches,
        MaxUsers = tenant.MaxUsers
        };
   }

   public async Task<bool> DeleteAsync(long id)
   {
            var tenant = await _context.Tenants
            .FirstOrDefaultAsync(c => c.TenantId == id);

        if (tenant == null)
            throw new Exception("Tenant not found.");

        tenant.IsActive = false;

        await _context.SaveChangesAsync();
        return true;
   }
}