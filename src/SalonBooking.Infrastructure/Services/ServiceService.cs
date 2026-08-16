using SalonBooking.Application.Features.Service.DTOs;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Common;
using SalonBooking.Persistence.Context;
using SalonBooking.Domain.Entities;
using SalonBooking.Infrastructure.Authentication;
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
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t =>
                t.TenantId == _currentUserService.TenantId);
                // && !t.IsDeleted
        if (tenant == null)
            throw new Exception("Tenant not found.");  

        var branch = await _context.Branches
        .FirstOrDefaultAsync(b =>
        b.BranchId == _currentUserService.BranchId &&
        b.TenantId == _currentUserService.TenantId);
        //&& !b.IsDeleted
        if (branch == null)
            throw new Exception("Branch not found.");      

        var exists = await _context.Services.AnyAsync(c =>
            c.TenantId == _currentUserService.TenantId &&
            c.ServiceName == request.ServiceName );   //&& !c.IsDeleted
        if (exists){     
             throw new Exception("Service already exists.."); 
        }
        var lastService = await _context.Services
           .OrderByDescending(c => c.ServiceId).FirstOrDefaultAsync();
                long  nextNumber = lastService == null
                    ? 1
                    : lastService.ServiceId + 1;
        var service = new Service
            {
                TenantId = _currentUserService.TenantId, 
                BranchId = _currentUserService.BranchId, 

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

        return new ServiceResponse
        {
            ServiceId = service.ServiceId,
            ServiceCode = service.ServiceCode,
            ServiceName = service.ServiceName,
            Description = service.Description,
            DurationMinutes = service.DurationMinutes,
            Price = service.Price,
            Cost = service.Cost,
            CommissionPercentage = service.CommissionPercentage,
            TenantId = service.TenantId,
            BranchId = service.BranchId
        };
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
    "ServiceName" => request.SortOrder == "desc"
        ? query.OrderByDescending(c => c.ServiceName)
        : query.OrderBy(c => c.ServiceName),

    "Description" => request.SortOrder == "desc"
        ? query.OrderByDescending(c => c.Description)
        : query.OrderBy(c => c.Description),

    "DurationMinutes" => request.SortOrder == "desc"
        ? query.OrderByDescending(c => c.DurationMinutes)
        : query.OrderBy(c => c.DurationMinutes),

    _ => query.OrderByDescending(c => c.ServiceId)
   };

   var totalRecords = await query.CountAsync();

   var services = await query
    .Skip((request.Page - 1) * request.PageSize)
    .Take(request.PageSize)
    .ToListAsync();

    var items = services.Select(s => new ServiceResponse
    {
    ServiceId = s.ServiceId,
    ServiceCode = s.ServiceCode,
    ServiceName = s.ServiceName,
    Description = s.Description,
    DurationMinutes = s.DurationMinutes,
    TenantId = s.TenantId,
    BranchId = s.BranchId
    }).ToList();

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
        return await _context.Services
            .Where(s => s.TenantId == _currentUserService.TenantId && s.IsActive ) //&& !s.IsDeleted
            .OrderBy(s => s.ServiceCode)
            .Select(s => new ServiceResponse
            {
                ServiceId = s.ServiceId,
                ServiceCode = s.ServiceCode,
                ServiceName = s.ServiceName,
                Description = s.Description,
                DurationMinutes = s.DurationMinutes,
                TenantId = s.TenantId,
                BranchId = s.BranchId
            })
            .ToListAsync();
    }

public async Task<ServiceResponse?> GetByIdAsync(long serviceId)
{
    return await _context.Services
        .Where(s => s.TenantId == _currentUserService.TenantId && s.ServiceId == serviceId ) //&& !s.IsDeleted
        .Select(s => new ServiceResponse
        {
            ServiceId = s.ServiceId,
            ServiceCode = s.ServiceCode,
            ServiceName = s.ServiceName,
            Description = s.Description,
            DurationMinutes = s.DurationMinutes,
            TenantId = s.TenantId,
            BranchId = s.BranchId
        })
        .FirstOrDefaultAsync();
}

public async Task<ServiceResponse> UpdateAsync(
  
    long serviceId,
    UpdateServiceRequest request)
{
    var service = await _context.Services
        .FirstOrDefaultAsync(s => s.ServiceId == serviceId);

    if (service == null)
        throw new Exception("Service not found.");

           var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t =>
                t.TenantId == _currentUserService.TenantId); // &&
                //!t.IsDeleted);

        if (tenant == null)
            throw new Exception("Tenant not found.");  

        var branch = await _context.Branches
        .FirstOrDefaultAsync(b =>
        b.BranchId == _currentUserService.BranchId &&
        b.TenantId == _currentUserService.TenantId); // &&
       // !b.IsDeleted);

        if (branch == null)
            throw new Exception("Branch not found.");      

        branch = await _context.Branches
            .FirstOrDefaultAsync(b =>
            b.BranchId == request.BranchId &&
            b.TenantId == _currentUserService.TenantId &&
            b.IsActive); // &&
         //   !b.IsDeleted);

            if (branch == null)
                throw new Exception("Invalid branch.");    

        var exists = await _context.Services.AnyAsync(s =>
            s.TenantId == _currentUserService.TenantId &&
            s.ServiceName == request.ServiceName &&
            s.ServiceId != serviceId); // &&
           // !s.IsDeleted);    
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
    service.BranchId = request.BranchId; 

    await _context.SaveChangesAsync();

    return new ServiceResponse
    {
        ServiceId = service.ServiceId,
        ServiceCode = service.ServiceCode,
        ServiceName = service.ServiceName,
        Description = service.Description,
        DurationMinutes = service.DurationMinutes,
        TenantId = service.TenantId,
        BranchId = service.BranchId
    };

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