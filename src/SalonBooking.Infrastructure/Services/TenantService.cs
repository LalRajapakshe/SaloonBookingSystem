using Microsoft.AspNetCore.Identity;
using SalonBooking.Application.Features.Identity;
using SalonBooking.Application.Features.Tenant.DTOs;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Common;
using SalonBooking.Persistence.Context;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Infrastructure.Authentication;
using SalonBooking.Infrastructure.Security;
using SalonBooking.Application.Security;
using Microsoft.EntityFrameworkCore;


namespace SalonBooking.Infrastructure.Services;

public class TenantService : ItenantService
{
    private readonly SalonBookingDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPasswordHasher<User> _passwords;

    public TenantService(
        SalonBookingDbContext context,
        ICurrentUserService currentUserService,
        IPasswordHasher<User> passwords)
    {
            _context = context;
            _currentUserService = currentUserService;
            _passwords = passwords;
    }
    public async Task<TenantResponse> CreateAsync(CreateTenantRequest request)
    {
        RequirePlatformAdministrator();
        var name = Required(request.TenantName, "Salon name", 150);
        var business = Required(request.BusinessName, "Business name", 200);
        var username = Required(request.AdministratorUsername, "Administrator username", 100);
        var password = request.AdministratorPassword ?? string.Empty;
        if (password.Length < 8)
        {
            throw new IdentityException("Password must be at least 8 characters.");
        }

        var firstName = Required(request.AdministratorFirstName, "Administrator first name", 100);
        var lastName = Required(request.AdministratorLastName, "Administrator last name", 100);
        var code = await NextTenantCodeAsync();
        var tenant = new Tenant
        {
            TenantCode = code,
            TenantName = name,
            BusinessName = business,
            BusinessRegistrationNo = request.BusinessRegistrationNo,
            ContactPerson = request.ContactPerson,
            PhoneNo = request.PhoneNo,
            Email = request.Email,
            Address = request.Address,
            LogoUrl = request.LogoUrl,
            SubscriptionPlan = SubscriptionPlan.Trial,
            SubscriptionStartDate = DateTime.UtcNow,
            SubscriptionEndDate = DateTime.UtcNow.AddYears(1),
            MaxBranches = request.MaxBranches > 0 ? request.MaxBranches : 1,
            MaxUsers = request.MaxUsers > 0 ? request.MaxUsers : 5,
            IsActive = true
        };
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();
        await AccessControlBootstrap.EnsureTenantRolesAsync(_context, tenant.TenantId);
        await AddSalonAdministratorAsync(tenant.TenantId, username, password, firstName, lastName, request.AdministratorEmail);
        return ToResponse(tenant);
    }

    public async Task<UserResponse> AssignSalonAdministratorAsync(
        long tenantId,
        string username,
        string password,
        string firstName,
        string lastName,
        string? email,
        CancellationToken cancellationToken = default)
    {
        RequirePlatformAdministrator();
        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.IsActive && !item.IsDeleted, cancellationToken);
        if (tenant == null)
        {
            throw new KeyNotFoundException();
        }

        if (password.Length < 8)
        {
            throw new IdentityException("Password must be at least 8 characters.");
        }

        var user = await AddSalonAdministratorAsync(
            tenantId,
            Required(username, "Username", 100),
            password,
            Required(firstName, "First name", 100),
            Required(lastName, "Last name", 100),
            email,
            cancellationToken);
        return new UserResponse
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            TenantId = user.TenantId,
            IsActive = user.IsActive
        };
    }

  public async Task<PagedResult<TenantResponse>> GetTenantsAsync(TenantQueryRequest request)
   {
    var tenantId = _currentUserService.RequireTenantId();
    var query = _context.Tenants.Where(c => c.IsActive && c.TenantId == tenantId).AsQueryable();
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

    public async Task<PagedResult<TenantResponse>> ListForPlatformAsync(
        TenantQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        RequirePlatformAdministrator();
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 10 : Math.Min(request.PageSize, 100);
        var query = _context.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => !item.IsDeleted);
        if (request.IsActive.HasValue)
        {
            query = query.Where(item => item.IsActive == request.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item =>
                item.TenantCode.Contains(search) ||
                item.TenantName.Contains(search) ||
                item.BusinessName.Contains(search) ||
                (item.ContactPerson != null && item.ContactPerson.Contains(search)) ||
                (item.PhoneNo != null && item.PhoneNo.Contains(search)) ||
                (item.Email != null && item.Email.Contains(search)));
        }

        query = request.SortBy.ToLowerInvariant() switch
        {
            "tenantcode" => request.SortOrder == "desc"
                ? query.OrderByDescending(item => item.TenantCode)
                : query.OrderBy(item => item.TenantCode),
            "tenantname" => request.SortOrder == "desc"
                ? query.OrderByDescending(item => item.TenantName)
                : query.OrderBy(item => item.TenantName),
            "businessname" => request.SortOrder == "desc"
                ? query.OrderByDescending(item => item.BusinessName)
                : query.OrderBy(item => item.BusinessName),
            _ => query.OrderBy(item => item.TenantId)
        };

        var totalRecords = await query.CountAsync(cancellationToken);
        var tenants = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<TenantResponse>
        {
            Page = page,
            PageSize = pageSize,
            TotalRecords = totalRecords,
            TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize),
            Items = tenants.Select(ToResponse).ToList()
        };
    }

    public async Task<TenantResponse?> GetByIdAsync(long id)
    {

        var tenantId = _currentUserService.RequireTenantId();
        return await _context.Tenants
        .Where(c => c.TenantId == id && c.TenantId == tenantId)
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
        var tenantId = _currentUserService.RequireTenantId();
        return await _context.Tenants
        .Where(c => c.IsActive && c.TenantId == tenantId)
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
        RequirePlatformAdministrator();
        var tenant = await _context.Tenants
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(c => c.TenantId == id && !c.IsDeleted);

        if (tenant == null)
            throw new KeyNotFoundException(); 
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
            RequirePlatformAdministrator();
            var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == id && !c.IsDeleted);

        if (tenant == null)
            throw new KeyNotFoundException();

        tenant.IsActive = false;

        await _context.SaveChangesAsync();
        return true;
   }

    private void RequirePlatformAdministrator()
    {
        if (!_currentUserService.IsPlatformAdministrator)
        {
            throw new UnauthorizedAccessException("Only a system administrator can perform this action.");
        }
    }

    private async Task<string> NextTenantCodeAsync()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var max = await _context.Tenants.IgnoreQueryFilters().MaxAsync(item => (long?)item.TenantId) ?? 0;
            var code = $"TEN{(max + 1 + attempt):D6}";
            var taken = await _context.Tenants.IgnoreQueryFilters().AnyAsync(item => item.TenantCode == code);
            if (!taken)
            {
                return code;
            }
        }

        throw new IdentityException("A salon code could not be reserved.");
    }

    private async Task<User> AddSalonAdministratorAsync(
        long tenantId,
        string username,
        string password,
        string firstName,
        string lastName,
        string? email,
        CancellationToken cancellationToken = default)
    {
        await AccessControlBootstrap.EnsureTenantRolesAsync(_context, tenantId, cancellationToken);
        var taken = await _context.Users.IgnoreQueryFilters().AnyAsync(
            item => item.TenantId == tenantId && item.Username == username && !item.IsDeleted,
            cancellationToken);
        if (taken)
        {
            throw new IdentityException("That username is already used in this salon.");
        }

        var role = await _context.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(
            item => item.TenantId == tenantId &&
                item.RoleCode == PermissionCatalog.AdminRoleCode &&
                item.IsActive &&
                !item.IsDeleted,
            cancellationToken);
        if (role == null)
        {
            throw new IdentityException("The salon administrator role is not available.");
        }

        var user = new User
        {
            TenantId = tenantId,
            BranchId = null,
            UserCategory = UserCategory.General,
            Username = username,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Role = "Admin",
            IsPlatformAdministrator = false,
            IsActive = true
        };
        user.PasswordHash = _passwords.HashPassword(user, password);
        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);
        _context.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = role.RoleId });
        await _context.SaveChangesAsync(cancellationToken);
        return user;
    }

    private static string Required(string? value, string label, int max)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length < 1 || trimmed.Length > max)
        {
            throw new IdentityException($"{label} is required and must be {max} characters or fewer.");
        }

        return trimmed;
    }

    private static TenantResponse ToResponse(Tenant tenant)
    {
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
}