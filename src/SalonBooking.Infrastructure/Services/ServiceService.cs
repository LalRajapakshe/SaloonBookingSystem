using SalonBooking.Application.Features.Service.DTOs;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Common;
using SalonBooking.Persistence.Context;
using SalonBooking.Domain.Entities;
using SalonBooking.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace SalonBooking.Infrastructure.Services;

public class ServiceService : IServiceService
{
    private readonly SalonBookingDbContext _context;
     private readonly ICurrentUserService _currentUserService;

    public ServiceService(SalonBookingDbContext context, ICurrentUserService currentUserService)
    {
            _context = context;
            _currentUserService = currentUserService;
    }
    public async Task<ServiceResponse> CreateAsync(CreateServiceRequest request)
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

        var category = await _context.ServiceCategories
            .FirstOrDefaultAsync(item =>
                item.ServiceCategoryId == request.ServiceCategoryId &&
                item.TenantId == tenantId &&
                item.IsActive);
        if (category == null)
            throw new Exception("Service category not found.");

        var exists = await _context.Services.AnyAsync(c =>
            c.TenantId == tenantId &&
            c.BranchId == branchId &&
            c.ServiceName == request.ServiceName);
        if (exists){     
             throw new Exception("Service already exists.."); 
        }
        var lastService = await _context.Services
           .IgnoreQueryFilters()
           .Where(item => item.TenantId == tenantId)
           .OrderByDescending(c => c.ServiceId).FirstOrDefaultAsync();
                long  nextNumber = lastService == null
                    ? 1
                    : lastService.ServiceId + 1;
        var service = new Service
            {
                TenantId = tenantId, 
                BranchId = branchId,
                ServiceCategoryId = request.ServiceCategoryId, 

                ServiceCode = $"SER{nextNumber:D6}", 
               // ServiceCode = $"SER{DateTime.Now.Ticks}",
                ServiceName = request.ServiceName,
                Description = request.Description,
                DurationMinutes = request.DurationMinutes,
                Price = request.Price,
                Cost = request.Cost,
                CommissionPercentage = request.CommissionPercentage,
            };

        _context.Services.Add(service);
        await _context.SaveChangesAsync();

        return ServiceResponse.From(service);
    }

  public async Task<PagedResult<ServiceResponse>> GetServicesAsync(ServiceQueryRequest request)
  {
    var query = _context.Services.Where(s => s.IsActive &&
    s.TenantId == _currentUserService.TenantId &&
        !s.IsDeleted).AsQueryable();
    if (!string.IsNullOrWhiteSpace(request.Search))
    {
    query = query.Where(s =>
        s.ServiceName.Contains(request.Search) ||
        s.Description.Contains(request.Search));
      //  s.DurationMinutes.Contains(request.Search));
    }
    //if (!string.IsNullOrWhiteSpace(request.Gender))
   // {
  //  query = query.Where(c => c.Gender == request.Gender);
  //  }

    query = request.SortBy.ToLower() switch
    {
    "servicename" => request.SortOrder == "desc"
        ? query.OrderByDescending(c => c.ServiceName)
        : query.OrderBy(c => c.ServiceName),

    "description" => request.SortOrder == "desc"
        ? query.OrderByDescending(c => c.Description)
        : query.OrderBy(c => c.Description),

    "durationminutes" => request.SortOrder == "desc"
        ? query.OrderByDescending(c => c.DurationMinutes)
        : query.OrderBy(c => c.DurationMinutes),

    "price" => request.SortOrder == "desc"
        ? query.OrderByDescending(c => c.Price)
        : query.OrderBy(c => c.Price),

    _ => query.OrderByDescending(c => c.ServiceId)
   };

   var totalRecords = await query.CountAsync();

   var services = await query
    .Skip((request.Page - 1) * request.PageSize)
    .Take(request.PageSize)
    .ToListAsync();

    var items = services.Select(ServiceResponse.From).ToList();

    return new PagedResult<ServiceResponse>
    {
    Page = request.Page,
    PageSize = request.PageSize,
    TotalRecords = totalRecords,
    TotalPages = (int)Math.Ceiling((double)totalRecords / request.PageSize),
    Items = items
    };
  }

  public async Task<List<ServiceResponse>> GetAllAsync()
    {
        var services = await _context.Services
            .Where(s => s.TenantId == _currentUserService.TenantId && s.IsActive ) //&& !s.IsDeleted
            .OrderBy(s => s.ServiceCode)
            .ToListAsync();

        return services.Select(ServiceResponse.From).ToList();
    }

public async Task<ServiceResponse?> GetByIdAsync(long serviceId)
{
    var service = await _context.Services
        .FirstOrDefaultAsync(s =>
            s.TenantId == _currentUserService.TenantId &&
            s.ServiceId == serviceId);

    return service == null ? null : ServiceResponse.From(service);
}

public async Task<ServiceResponse> UpdateAsync(
  
    long serviceId,
    UpdateServiceRequest request)
{
    var tenantId = _currentUserService.RequireTenantId();
    var branchId = _currentUserService.ResolveBranchId(request.BranchId);

    var service = await _context.Services
        .FirstOrDefaultAsync(s =>
            s.ServiceId == serviceId &&
            s.TenantId == tenantId);

    if (service == null)
        throw new Exception("Service not found.");

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

        var category = await _context.ServiceCategories
            .FirstOrDefaultAsync(item =>
                item.ServiceCategoryId == request.ServiceCategoryId &&
                item.TenantId == tenantId &&
                item.IsActive);
        if (category == null)
            throw new Exception("Service category not found.");

        var exists = await _context.Services.AnyAsync(s =>
            s.TenantId == tenantId &&
            s.BranchId == branchId &&
            s.ServiceName == request.ServiceName &&
            s.ServiceId != serviceId);
        if (exists){    
             throw new Exception("Service already exists.."); 
        }

    service.ServiceName = request.ServiceName;
    service.Description = request.Description;
    service.DurationMinutes = request.DurationMinutes;
    service.Price = request.Price;
    service.Cost = request.Cost;
    service.CommissionPercentage = request.CommissionPercentage;
    service.IsActive = request.IsActive;
    //service.TenantId = request.TenantId;
    service.BranchId = branchId;
    service.ServiceCategoryId = request.ServiceCategoryId; 

    await _context.SaveChangesAsync();

    return ServiceResponse.From(service);

}


public async Task DeleteAsync(long serviceId)
    {
        var service = await _context.Services
            .FirstOrDefaultAsync(s => s.ServiceId == serviceId && s.TenantId == _currentUserService.TenantId);

        if (service == null)
            throw new Exception("Service not found.");

        service.IsActive = false;
        service.IsDeleted = true;

        await _context.SaveChangesAsync();
    }
}