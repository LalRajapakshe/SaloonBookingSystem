using SalonBooking.Application.Features.ServiceCategory.DTOs;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Common;
using SalonBooking.Persistence.Context;
using SalonBooking.Domain.Entities;
using SalonBooking.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;

namespace SalonBooking.Infrastructure.Services;

public class ServiceCategoryService : IServiceCategoryService
{
    private readonly SalonBookingDbContext _context;
     private readonly ICurrentUserService _currentUserService;

    public ServiceCategoryService(SalonBookingDbContext context, ICurrentUserService currentUserService)
    {
            _context = context;
            _currentUserService = currentUserService;
    }
    public async Task<ServiceCategoryResponse> CreateAsync(CreateServiceCategoryRequest request)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t =>
                t.TenantId == _currentUserService.TenantId);

        if (tenant == null)
            throw new Exception("Tenant not found.");  

     //  var branch = await _context.Branches
     //   .FirstOrDefaultAsync(b =>
     //   b.BranchId == _currentUserService.BranchId &&
    //    b.TenantId == _currentUserService.TenantId);
        //&& !b.IsDeleted
    //    if (branch == null)
    //        throw new Exception("Branch not found.");      

        var exists = await _context.ServiceCategories.AnyAsync(c =>
            c.TenantId == _currentUserService.TenantId &&
            c.CategoryName == request.CategoryName );   //&& !c.IsDeleted
        if (exists){     
             throw new Exception("Service category already exists.."); 
        }
        var lastServiceCategory = await _context.ServiceCategories
           .OrderByDescending(c => c.ServiceCategoryId).FirstOrDefaultAsync();
                long  nextNumber = lastServiceCategory == null
                    ? 1
                    : lastServiceCategory.ServiceCategoryId + 1;
        var serviceCategory = new ServiceCategory
            {
                TenantId = _currentUserService.TenantId, 
              //  BranchId = _currentUserService.BranchId, 

                CategoryCode = $"CAT{nextNumber:D6}", 
               // CategoryCode = $"CAT{DateTime.Now.Ticks}",
                CategoryName = request.CategoryName,
                Description = request.Description,
                DisplayOrder = request.DisplayOrder,

              //  IsActive = true
                //  reatedDate = DateTime.UtcNow
            };

        _context.ServiceCategories.Add(serviceCategory);
        await _context.SaveChangesAsync();

        return new ServiceCategoryResponse
        {
            ServiceCategoryId = serviceCategory.ServiceCategoryId,
            CategoryCode = serviceCategory.CategoryCode,
            CategoryName = serviceCategory.CategoryName,
            Description = serviceCategory.Description,
            DisplayOrder = serviceCategory.DisplayOrder,
            TenantId = serviceCategory.TenantId,
           // BranchId = serviceCategory.BranchId
        };
    }

  public async Task<PagedResult<ServiceCategoryResponse>> GetServiceCategoriesAsync(ServiceCategoryQueryRequest request)
  {
    var query = _context.ServiceCategories.Where(c => c.IsActive &&
    c.TenantId == _currentUserService.TenantId &&
        !c.IsDeleted).AsQueryable();
    if (!string.IsNullOrWhiteSpace(request.Search))
    {
    query = query.Where(c =>
        c.CategoryName.Contains(request.Search) ||
        c.Description.Contains(request.Search));
    }
  //  if (!string.IsNullOrWhiteSpace(request.Gender))
  //  {
  //  query = query.Where(c => c.Gender == request.Gender);
 //   }

    query = request.SortBy.ToLower() switch
    {
    "CategoryName" => request.SortOrder == "desc"
        ? query.OrderByDescending(c => c.CategoryName)
        : query.OrderBy(c => c.CategoryName),

    "Description" => request.SortOrder == "desc"
        ? query.OrderByDescending(c => c.CategoryName)
       // : query.OrderBy(c => c.Description),
        : query.OrderBy(c => c.CategoryName),



    _ => query.OrderByDescending(c => c.ServiceCategoryId)
   };

   var totalRecords = await query.CountAsync();

   var ServiceCategorys = await query
    .Skip((request.Page - 1) * request.PageSize)
    .Take(request.PageSize)
    .ToListAsync();

    var items = ServiceCategorys.Select(c => new ServiceCategoryResponse
    {
    ServiceCategoryId = c.ServiceCategoryId,
    CategoryCode = c.CategoryCode,
    CategoryName = c.CategoryName,
    Description = c.Description,
    DisplayOrder = c.DisplayOrder,
    TenantId = c.TenantId,
   // BranchId = c.BranchId
    }).ToList();

    return new PagedResult<ServiceCategoryResponse>
    {
    Page = request.Page,
    PageSize = request.PageSize,
    TotalRecords = totalRecords,
    TotalPages = (int)Math.Ceiling((double)totalRecords / request.PageSize),
    Items = items
    };
  }

  public async Task<List<ServiceCategoryResponse>> GetAllAsync()
    {
        return await _context.ServiceCategories
            .Where(c => c.TenantId == _currentUserService.TenantId && c.IsActive ) //&& !c.IsDeleted
            .OrderBy(c => c.CategoryCode)
            .Select(c => new ServiceCategoryResponse
            {
                ServiceCategoryId = c.ServiceCategoryId,
                CategoryCode = c.CategoryCode,
                CategoryName = c.CategoryName,
                Description = c.Description,
                DisplayOrder = c.DisplayOrder,
                TenantId = c.TenantId,
             //   BranchId = c.BranchId
            })
            .ToListAsync();
    }

public async Task<ServiceCategoryResponse?> GetByIdAsync(long serviceCategoryId)
{
    return await _context.ServiceCategories
        .Where(c => c.TenantId == _currentUserService.TenantId && c.ServiceCategoryId == serviceCategoryId ) //&& !c.IsDeleted
        .Select(c => new ServiceCategoryResponse
        {
            ServiceCategoryId = c.ServiceCategoryId,
            CategoryCode = c.CategoryCode,
            CategoryName = c.CategoryName,
            Description = c.Description,
            DisplayOrder = c.DisplayOrder,
            TenantId = c.TenantId,
          //  BranchId = c.BranchId
        })
        .FirstOrDefaultAsync();
}

public async Task<ServiceCategoryResponse> UpdateAsync(
  
    long serviceCategoryId,
    UpdateServiceCategoryRequest request)
{
    var serviceCategory = await _context.ServiceCategories
        .FirstOrDefaultAsync(c => c.ServiceCategoryId == serviceCategoryId);

    if (serviceCategory == null)
        throw new Exception("Service category not found.");

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

    //    branch = await _context.Branches
    //        .FirstOrDefaultAsync(b =>
    //        b.BranchId == request.BranchId &&
    //        b.TenantId == _currentUserService.TenantId &&
    //       !b.IsDeleted); // &&
         //   !b.IsDeleted);
    //        if (branch == null)
    //            throw new Exception("Invalid branch.");    

        var exists = await _context.ServiceCategories.AnyAsync(c =>
            c.TenantId == _currentUserService.TenantId &&
            c.CategoryName == request.CategoryName &&
            c.ServiceCategoryId != serviceCategoryId); // &&
           // !c.IsDeleted);    
        if (exists){    
             throw new Exception("Service category already exists."); 
        }

    serviceCategory.CategoryName = request.CategoryName;
    serviceCategory.Description = request.Description;
    serviceCategory.DisplayOrder = request.DisplayOrder;

   //customer.TenantId = request.TenantId;
   // serviceCategory.BranchId = request.BranchId; 

    await _context.SaveChangesAsync();

    return new ServiceCategoryResponse
    {
        ServiceCategoryId = serviceCategory.ServiceCategoryId,
        CategoryCode = serviceCategory.CategoryCode,
        CategoryName = serviceCategory.CategoryName,
        Description = serviceCategory.Description,
        DisplayOrder = serviceCategory.DisplayOrder,
        TenantId = serviceCategory.TenantId,
    //    BranchId = serviceCategory.BranchId
    };
}

public async Task DeleteAsync(long serviceCategoryId)
    {
        var serviceCategory = await _context.ServiceCategories
            .FirstOrDefaultAsync(c => c.ServiceCategoryId == serviceCategoryId  &&
    c.TenantId == _currentUserService.TenantId);

        if (serviceCategory == null)
            throw new Exception("Service category not found.");

        serviceCategory.IsActive = false;
        serviceCategory.IsDeleted = true;

        await _context.SaveChangesAsync();
    }
}