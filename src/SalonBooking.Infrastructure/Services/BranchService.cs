using SalonBooking.Application.Features.Branch.DTOs;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Common;
using SalonBooking.Persistence.Context;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace SalonBooking.Infrastructure.Services;

public class BranchService : IBranchService
{
    private readonly SalonBookingDbContext _context;

    public BranchService(SalonBookingDbContext context)
    {
            _context = context;
    }
    public async Task<BranchResponse> CreateAsync(CreateBranchRequest request)
    {
        var lastTenant = await _context.Branches
        .OrderByDescending(t => t.BranchId)
        .FirstOrDefaultAsync();

        long nextNumber = lastTenant == null
            ? 1
            : lastTenant.TenantId + 1;

        string branchCode = $"TEN{nextNumber:D6}";
        
        var branch = new Branch
        {
            BranchCode = branchCode,
            BranchName = request.BranchName,
            AddressLine1 = request.AddressLine1,
            AddressLine2  = request.AddressLine2,
            City = request.City,
            PhoneNo = request.PhoneNo,
            Email = request.Email,
            ManagerName = request.ManagerName,
            IsHeadOffice = request.IsHeadOffice,
          //  IsActive  = request.,
          //  IsDeleted = request.false,
            TenantId = request.TenantId

        };
        _context.Branches.Add(branch);

        await _context.SaveChangesAsync();

        return new BranchResponse
        {
            TenantId = branch.TenantId,
            BranchCode = branch.BranchCode,
            BranchName = branch.BranchName,
            AddressLine1 = branch.AddressLine1,
            AddressLine2 = branch.AddressLine2,
            City = branch.City,
            PhoneNo = branch.PhoneNo,
            Email = branch.Email,
            ManagerName = branch.ManagerName,
            IsHeadOffice = branch.IsHeadOffice
        };
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
        BranchCode = c.BranchCode,
        BranchName = c.BranchName,
        AddressLine1 = c.AddressLine1,
        AddressLine2 = c.AddressLine2,
        City = c.City,
        PhoneNo = c.PhoneNo,
        Email = c.Email,
        ManagerName = c.ManagerName,
        IsHeadOffice = c.IsHeadOffice
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

        return await _context.Branches
        .Where(c => c.BranchId == id)
        .Select(c => new BranchResponse
        {
            TenantId = c.TenantId,
            BranchCode = c.BranchCode,
            BranchName = c.BranchName,
            AddressLine1 = c.AddressLine1,
            AddressLine2 = c.AddressLine2,
            City = c.City,
            PhoneNo = c.PhoneNo,
            Email = c.Email,
            ManagerName = c.ManagerName,
            IsHeadOffice = c.IsHeadOffice
        })
        .FirstOrDefaultAsync();
    }

   public async Task<BranchResponse> GetAllAsync(int page, int pageSize)
    {
        return await _context.Branches
        .Where(c => c.IsActive)
        .OrderBy(c => c.BranchCode)
        .Select(c => new BranchResponse
        {
            TenantId = c.TenantId,
            BranchCode = c.BranchCode,
            BranchName = c.BranchName,
            AddressLine1 = c.AddressLine1,
            AddressLine2 = c.AddressLine2,
            City = c.City,
            PhoneNo = c.PhoneNo,
            Email = c.Email,
            ManagerName = c.ManagerName,
            IsHeadOffice = c.IsHeadOffice
        })
        .FirstOrDefaultAsync();
    }

   public async Task<BranchResponse?> UpdateAsync(long id, UpdateBranchRequest request)
   {

        var branch = await _context.Branches
        .FirstOrDefaultAsync(c => c.BranchId == id);

        if (branch == null)
            throw new Exception("Tenant not found.");

        branch.BranchId =id; //request.TenantId; 
        branch.TenantId = request.TenantId;
        branch.BranchName = request.BranchName;
        branch.AddressLine1 = request.AddressLine1;
        branch.AddressLine2 = request.AddressLine2;
        branch.City  = request.City;
        branch.PhoneNo = request.PhoneNo;
        branch.Email = request.Email;
        branch.ManagerName = request.ManagerName;
        branch.IsHeadOffice = request.IsHeadOffice;

        await _context.SaveChangesAsync();

            return new BranchResponse
        {
            TenantId = branch.TenantId,
            BranchCode = "",
            BranchName = branch.BranchName,
            AddressLine1 = branch.AddressLine1,
            AddressLine2 = branch.AddressLine2,
            City = branch.City,
            PhoneNo = branch.PhoneNo,
            Email = branch.Email,
            ManagerName = branch.ManagerName,
            IsHeadOffice = branch.IsHeadOffice
        };
   }

   public async Task<bool> DeleteAsync(long id)
   {
            var branch = await _context.Branches
            .FirstOrDefaultAsync(c => c.BranchId == id);

        if (branch == null)
            throw new Exception("Tenant not found.");

        branch.IsActive = false;

        await _context.SaveChangesAsync();
        return true;
   }
}