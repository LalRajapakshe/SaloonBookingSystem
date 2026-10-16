using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Identity;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Security;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Infrastructure.Authentication;
using SalonBooking.Infrastructure.Security;
using SalonBooking.Persistence.Context;

namespace SalonBooking.Infrastructure.Services;

public sealed class RoleAdminService : IRoleAdminService
{
    private static readonly HashSet<string> BuiltInCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        PermissionCatalog.AdminRoleCode,
        PermissionCatalog.StaffRoleCode,
        PermissionCatalog.CustomerRoleCode
    };

    private readonly SalonBookingDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RoleAdminService(SalonBookingDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<RoleResponse>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var roles = await Roles().Where(role => role.TenantId == tenantId).OrderBy(role => role.RoleName).ToListAsync(cancellationToken);
        return roles.Select(Map).ToList();
    }

    public async Task<RoleResponse> GetRoleAsync(long roleId, CancellationToken cancellationToken = default)
    {
        return Map(await RequireRoleAsync(roleId, cancellationToken));
    }

    public async Task<RoleResponse> CreateAsync(SaveRoleRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var code = RequiredCode(request.RoleCode);
        await EnsureCodeAvailableAsync(tenantId, code, null, cancellationToken);
        var role = new Role
        {
            TenantId = tenantId,
            RoleCode = code,
            RoleName = RequiredName(request.RoleName),
            Description = Clean(request.Description),
            IsActive = true
        };
        _context.Roles.Add(role);
        await _context.SaveChangesAsync(cancellationToken);
        if (!request.IsActive)
        {
            role.IsActive = false;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Map(await RequireRoleAsync(role.RoleId, cancellationToken));
    }

    public async Task<RoleResponse> UpdateAsync(long roleId, SaveRoleRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var role = await RequireRoleAsync(roleId, cancellationToken);
        var code = RequiredCode(request.RoleCode);
        if (BuiltInCodes.Contains(role.RoleCode) && !string.Equals(role.RoleCode, code, StringComparison.OrdinalIgnoreCase))
        {
            throw new IdentityException("The built-in role code cannot be changed.");
        }

        await EnsureCodeAvailableAsync(tenantId, code, role.RoleId, cancellationToken);
        role.RoleCode = code;
        role.RoleName = RequiredName(request.RoleName);
        role.Description = Clean(request.Description);
        role.IsActive = request.IsActive;
        var stillGrants = role.IsActive &&
                          role.RolePermissions.Any(item =>
                              item.Permission is { IsActive: true, IsDeleted: false, PermissionCode: PermissionCatalog.UsersManage });
        await EnsureRoleStillLeavesAManagerAsync(tenantId, role.RoleId, stillGrants, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Map(await RequireRoleAsync(role.RoleId, cancellationToken));
    }

    public async Task<RoleResponse> SetPermissionsAsync(
        long roleId,
        IReadOnlyCollection<long> permissionIds,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var role = await RequireRoleAsync(roleId, cancellationToken);
        var ids = permissionIds.Where(id => id > 0).Distinct().ToList();
        var permissions = await _context.Permissions
            .Where(permission => ids.Contains(permission.PermissionId) && permission.IsActive && !permission.IsDeleted)
            .ToListAsync(cancellationToken);
        if (permissions.Count != ids.Count)
        {
            throw new IdentityException("One or more permissions are not in the catalog.");
        }

        if (string.Equals(role.RoleCode, PermissionCatalog.CustomerRoleCode, StringComparison.OrdinalIgnoreCase) &&
            permissions.Any(permission => PermissionCatalog.IsStaffCode(permission.PermissionCode)))
        {
            throw new IdentityException("The customer role cannot include staff permissions.");
        }

        await AccessReader.EnsureMayGrantAsync(
            _context,
            _currentUser,
            permissions.Select(permission => permission.PermissionCode),
            cancellationToken);

        if (permissions.Any(permission => PermissionCatalog.IsStaffCode(permission.PermissionCode)))
        {
            var customerHoldsRole = await _context.Users.IgnoreQueryFilters().AnyAsync(
                user => !user.IsDeleted &&
                        user.UserCategory == UserCategory.Customer &&
                        user.UserRoles.Any(userRole => userRole.RoleId == role.RoleId),
                cancellationToken);
            if (customerHoldsRole)
            {
                throw new IdentityException("A role assigned to a customer cannot include staff permissions.");
            }
        }

        var existing = await _context.RolePermissions
            .Where(item => item.RoleId == role.RoleId)
            .ToListAsync(cancellationToken);
        var wanted = permissions.Select(permission => permission.PermissionId).ToHashSet();
        _context.RolePermissions.RemoveRange(existing.Where(item => !wanted.Contains(item.PermissionId)));
        var present = existing.Select(item => item.PermissionId).ToHashSet();
        foreach (var permission in permissions.Where(permission => !present.Contains(permission.PermissionId)))
        {
            _context.RolePermissions.Add(new RolePermission
            {
                RoleId = role.RoleId,
                PermissionId = permission.PermissionId
            });
        }

        var stillGrants = role.IsActive &&
                          permissions.Any(permission => permission.PermissionCode == PermissionCatalog.UsersManage);
        await EnsureRoleStillLeavesAManagerAsync(tenantId, role.RoleId, stillGrants, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Map(await RequireRoleAsync(role.RoleId, cancellationToken));
    }

    public async Task<IReadOnlyList<PermissionResponse>> GetPermissionCatalogAsync(CancellationToken cancellationToken = default)
    {
        _ = _currentUser.RequireTenantId();
        var permissions = await _context.Permissions
            .AsNoTracking()
            .Where(permission => permission.IsActive && !permission.IsDeleted)
            .OrderBy(permission => permission.PermissionCode)
            .ToListAsync(cancellationToken);

        return permissions.Select(permission =>
        {
            var defined = PermissionCatalog.All.FirstOrDefault(item =>
                string.Equals(item.Code, permission.PermissionCode, StringComparison.Ordinal));
            return new PermissionResponse
            {
                PermissionId = permission.PermissionId,
                PermissionCode = permission.PermissionCode,
                PermissionName = permission.PermissionName,
                Description = permission.Description,
                Area = defined?.Area ?? "Other"
            };
        }).OrderBy(item => item.Area).ThenBy(item => item.PermissionName).ToList();
    }

    private IQueryable<Role> Roles()
    {
        return _context.Roles
            .AsNoTracking()
            .Include(role => role.RolePermissions)
            .Where(role => !role.IsDeleted);
    }

    private async Task<Role> RequireRoleAsync(long roleId, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.RequireTenantId();
        var role = await _context.Roles
            .Include(item => item.RolePermissions)
            .ThenInclude(item => item.Permission)
            .FirstOrDefaultAsync(item => item.RoleId == roleId && !item.IsDeleted, cancellationToken);

        if (role == null || role.TenantId != tenantId)
        {
            throw new KeyNotFoundException();
        }

        return role;
    }

    private async Task EnsureCodeAvailableAsync(
        long tenantId,
        string code,
        long? exceptRoleId,
        CancellationToken cancellationToken)
    {
        var taken = await _context.Roles.IgnoreQueryFilters().AnyAsync(
            role => role.TenantId == tenantId &&
                    role.RoleCode == code &&
                    !role.IsDeleted &&
                    role.RoleId != exceptRoleId,
            cancellationToken);
        if (taken)
        {
            throw new IdentityException("That role code is already used in this salon.");
        }
    }

    private async Task EnsureRoleStillLeavesAManagerAsync(
        long tenantId,
        long roleId,
        bool grantsManage,
        CancellationToken cancellationToken)
    {
        var managers = await (
            from user in _context.Users.IgnoreQueryFilters().AsNoTracking()
            join userRole in _context.UserRoles.AsNoTracking() on user.UserId equals userRole.UserId
            join assigned in _context.Roles.IgnoreQueryFilters().AsNoTracking() on userRole.RoleId equals assigned.RoleId
            join rolePermission in _context.RolePermissions.AsNoTracking() on assigned.RoleId equals rolePermission.RoleId
            join permission in _context.Permissions.AsNoTracking() on rolePermission.PermissionId equals permission.PermissionId
            where user.TenantId == tenantId &&
                  user.IsActive &&
                  !user.IsDeleted &&
                  user.UserCategory != UserCategory.Customer &&
                  assigned.RoleId != roleId &&
                  assigned.TenantId == tenantId &&
                  assigned.IsActive &&
                  !assigned.IsDeleted &&
                  permission.PermissionCode == PermissionCatalog.UsersManage &&
                  permission.IsActive &&
                  !permission.IsDeleted
            select user.UserId
        ).Distinct().ToListAsync(cancellationToken);

        if (grantsManage)
        {
            var holders = await _context.UserRoles.AsNoTracking()
                .Where(item => item.RoleId == roleId)
                .Join(
                    _context.Users.IgnoreQueryFilters().AsNoTracking().Where(user =>
                        user.TenantId == tenantId &&
                        user.IsActive &&
                        !user.IsDeleted &&
                        user.UserCategory != UserCategory.Customer),
                    item => item.UserId,
                    user => user.UserId,
                    (item, user) => user.UserId)
                .ToListAsync(cancellationToken);
            managers.AddRange(holders);
        }

        if (managers.Distinct().Count() == 0)
        {
            throw new IdentityException("The salon must keep at least one active user who can manage users.");
        }
    }

    private static RoleResponse Map(Role role)
    {
        return new RoleResponse
        {
            RoleId = role.RoleId,
            RoleCode = role.RoleCode,
            RoleName = role.RoleName,
            Description = role.Description,
            IsActive = role.IsActive,
            PermissionIds = role.RolePermissions.Select(item => item.PermissionId).Distinct().OrderBy(id => id).ToList()
        };
    }

    private static string RequiredCode(string? code)
    {
        var trimmed = code?.Trim().ToUpperInvariant() ?? string.Empty;
        if (trimmed.Length is < 1 or > 20 || trimmed.Any(character => !char.IsLetterOrDigit(character)))
        {
            throw new IdentityException("Role code is required, up to 20 letters or digits.");
        }

        return trimmed;
    }

    private static string RequiredName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is < 1 or > 100)
        {
            throw new IdentityException("Role name is required and must be 100 characters or fewer.");
        }

        return trimmed;
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > 300)
        {
            throw new IdentityException("Description must be 300 characters or fewer.");
        }

        return trimmed;
    }
}
