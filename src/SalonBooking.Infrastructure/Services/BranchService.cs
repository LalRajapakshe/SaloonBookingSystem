using SalonBooking.Application.Features.Branch.DTOs;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Common;
using SalonBooking.Persistence.Context;
using SalonBooking.Domain.Entities;
using SalonBooking.Application.Documents;
using SalonBooking.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace SalonBooking.Infrastructure.Services;

public class BranchService : IBranchService
{
    private readonly SalonBookingDbContext _context;
     private readonly ICurrentUserService _currentUserService;
    private readonly IDocumentNumberService _numbers;

    public BranchService(
        SalonBookingDbContext context,
        ICurrentUserService currentUserService,
        IDocumentNumberService numbers)
    {
            _context = context;
            _currentUserService = currentUserService;
            _numbers = numbers;
    }
    public async Task<BranchResponse> CreateAsync(CreateBranchRequest request)
    {
        if (!_currentUserService.IsPlatformAdministrator)
        {
            throw new UnauthorizedAccessException(
                "Only a system administrator can create a branch.");
        }

        var tenantId = request.TenantId > 0
            ? request.TenantId
            : _currentUserService.RequireTenantId();
        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.IsActive &&
                !item.IsDeleted);
        if (tenant == null)
            throw new KeyNotFoundException();

        var branchCount = await _context.Branches
            .IgnoreQueryFilters()
            .CountAsync(item => item.TenantId == tenantId && !item.IsDeleted);
        if (tenant.MaxBranches > 0 && branchCount >= tenant.MaxBranches)
        {
            throw new IdentityException("This salon has reached its branch limit.");
        }

        return await _numbers.ExecuteAsync(async () =>
        {
            var branch = new Branch
            {
                BranchCode = await _numbers.NextCodeAsync(tenantId, DocSeriesKind.Branch),
                BranchName = request.BranchName.Trim(),
                AddressLine1 = request.AddressLine1,
                AddressLine2 = request.AddressLine2,
                City = request.City,
                PhoneNo = request.PhoneNo,
                Email = request.Email,
                ManagerName = request.ManagerName,
                IsHeadOffice = request.IsHeadOffice,
                Highlight = CleanHighlight(request.Highlight),
                IsCustomerVisible = request.IsCustomerVisible,
                TenantId = tenantId
            };
            _context.Branches.Add(branch);
            await _context.SaveChangesAsync();

            return new BranchResponse
            {
                TenantId = branch.TenantId,
                BranchId = branch.BranchId,
                BranchCode = branch.BranchCode,
                BranchName = branch.BranchName,
                AddressLine1 = branch.AddressLine1,
                AddressLine2 = branch.AddressLine2,
                City = branch.City,
                PhoneNo = branch.PhoneNo,
                Email = branch.Email,
                ManagerName = branch.ManagerName,
                IsHeadOffice = branch.IsHeadOffice,
                Highlight = branch.Highlight,
                PhotoPath = branch.PhotoPath,
                IsCustomerVisible = branch.IsCustomerVisible,
                IsActive = branch.IsActive
            };
        });
    }

  public async Task<PagedResult<BranchResponse>> GetBranchAsync(BranchQueryRequest request)
   {
    var query = _context.Branches.Where(c => c.IsActive).AsQueryable();
    if (!string.IsNullOrWhiteSpace(request.Search))
    {
    query = query.Where(c =>
        c.BranchCode.Contains(request.Search) ||
        c.BranchName.Contains(request.Search) ||
        c.City.Contains(request.Search) ||
       // c.ContactPerson.Contains(request.Search) ||
        c.PhoneNo.Contains(request.Search) ||
        c.ManagerName.Contains(request.Search) ||
      //  c.IsHeadOffice.Contains(request.Search) ||
        c.Email.Contains(request.Search));
    }
   // if (!string.IsNullOrWhiteSpace(request.Gender))
   // {
   // query = query.Where(c => c.Gender == request.Gender);
   // }

    query = request.SortBy.ToLower() switch
    {
    "BranchCode" => request.SortOrder == "desc"
        ? query.OrderByDescending(c => c.BranchCode)
        : query.OrderBy(c => c.BranchCode),

    "BranchName" => request.SortOrder == "desc"
        ? query.OrderByDescending(c => c.BranchName)
        : query.OrderBy(c => c.BranchName),

    "City" => request.SortOrder == "desc"
        ? query.OrderByDescending(c => c.City)
        : query.OrderBy(c => c.City),

    "PhoneNo" => request.SortOrder == "desc"
        ? query.OrderByDescending(c => c.PhoneNo)
        : query.OrderBy(c => c.PhoneNo),

    "ManagerName" => request.SortOrder == "desc"
        ? query.OrderByDescending(c => c.ManagerName)
        : query.OrderBy(c => c.ManagerName),

    _ => query.OrderByDescending(c => c.BranchId)
   };

   var totalRecords = await query.CountAsync();

   var branch = await query
    .Skip((request.Page - 1) * request.PageSize)
    .Take(request.PageSize)
    .ToListAsync();

    var items = branch.Select(c => new BranchResponse
    {
        TenantId = c.TenantId,
        BranchId = c.BranchId,
        BranchCode = c.BranchCode,
        BranchName = c.BranchName,
        AddressLine1 = c.AddressLine1,
        AddressLine2 = c.AddressLine2,
        City = c.City,
        PhoneNo = c.PhoneNo,
        Email = c.Email,
        ManagerName = c.ManagerName,
        IsHeadOffice = c.IsHeadOffice,
        Highlight = c.Highlight,
        PhotoPath = c.PhotoPath,
        IsCustomerVisible = c.IsCustomerVisible,
        IsActive = c.IsActive
    }).ToList();

    return new PagedResult<BranchResponse>
    {
    Page = request.Page,
    PageSize = request.PageSize,
    TotalRecords = totalRecords,
    TotalPages = (int)Math.Ceiling((double)totalRecords / request.PageSize),
    Items = items
    };
    }

    public async Task<BranchResponse?> GetByIdAsync(long id)
    {
        var tenantId = _currentUserService.RequireTenantId();
        var branch = await _context.Branches.RequireOwnedAsync(
            _currentUserService,
            item => item.BranchId == id &&
                    item.TenantId == tenantId &&
                    !item.IsDeleted,
            item => item.BranchId);

        return Map(branch);
    }

   public async Task<BranchResponse> GetAllAsync(int page, int pageSize)
    {
        return await _context.Branches
        .Where(c => c.IsActive)
        .OrderBy(c => c.BranchCode)
        .Select(c => new BranchResponse
        {
            TenantId = c.TenantId,
            BranchId = c.BranchId,
            BranchCode = c.BranchCode,
            BranchName = c.BranchName,
            AddressLine1 = c.AddressLine1,
            AddressLine2 = c.AddressLine2,
            City = c.City,
            PhoneNo = c.PhoneNo,
            Email = c.Email,
            ManagerName = c.ManagerName,
            IsHeadOffice = c.IsHeadOffice,
            Highlight = c.Highlight,
            PhotoPath = c.PhotoPath,
            IsCustomerVisible = c.IsCustomerVisible,
            IsActive = c.IsActive
        })
        .FirstOrDefaultAsync();
    }

   public async Task<BranchResponse?> UpdateAsync(long id, UpdateBranchRequest request)
   {
        var tenantId = _currentUserService.RequireTenantId();
        var branch = await _context.Branches.RequireOwnedAsync(
            _currentUserService,
            item => item.BranchId == id &&
                    item.TenantId == tenantId &&
                    !item.IsDeleted,
            item => item.BranchId);

        branch.BranchName = request.BranchName;
        branch.AddressLine1 = request.AddressLine1;
        branch.AddressLine2 = request.AddressLine2;
        branch.City  = request.City;
        branch.PhoneNo = request.PhoneNo;
        branch.Email = request.Email;
        branch.ManagerName = request.ManagerName;
        branch.IsHeadOffice = request.IsHeadOffice;
        branch.Highlight = CleanHighlight(request.Highlight);
        branch.IsCustomerVisible = request.IsCustomerVisible;
        branch.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        return Map(branch);
   }

   public async Task<bool> DeleteAsync(long id)
   {
        var tenantId = _currentUserService.RequireTenantId();
        var branch = await _context.Branches.RequireOwnedAsync(
            _currentUserService,
            item => item.BranchId == id &&
                    item.TenantId == tenantId &&
                    !item.IsDeleted,
            item => item.BranchId);

        branch.IsActive = false;
        branch.IsDeleted = true;

        await _context.SaveChangesAsync();
        return true;
   }

    private static BranchResponse Map(Branch branch)
    {
        return new BranchResponse
        {
            TenantId = branch.TenantId,
            BranchId = branch.BranchId,
            BranchCode = branch.BranchCode,
            BranchName = branch.BranchName,
            AddressLine1 = branch.AddressLine1,
            AddressLine2 = branch.AddressLine2,
            City = branch.City,
            PhoneNo = branch.PhoneNo,
            Email = branch.Email,
            ManagerName = branch.ManagerName,
            IsHeadOffice = branch.IsHeadOffice,
            Highlight = branch.Highlight,
            PhotoPath = branch.PhotoPath,
            IsCustomerVisible = branch.IsCustomerVisible,
            IsActive = branch.IsActive
        };
    }

    private static string? CleanHighlight(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= 150 ? trimmed : trimmed[..150];
    }
}