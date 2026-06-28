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
    public async Task<TenantResponse?> GetByIdAsync(long id)
    {

        return null;
    }

   public async Task<PagedResult<TenantResponse>> GetAllAsync(int page, int pageSize)
       {

        return null;
    }

   public async Task<TenantResponse?> UpdateAsync(long id, UpdateTenantRequest request)
       {

        return null;
    }

   public async Task<bool> DeleteAsync(long id)
   {
    return true;
   }
}