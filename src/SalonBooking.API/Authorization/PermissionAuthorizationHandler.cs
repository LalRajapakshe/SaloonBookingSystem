using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Security;
using SalonBooking.Domain.Enums;
using SalonBooking.Persistence.Context;

namespace SalonBooking.API.Authorization;

public sealed class PermissionAuthorizationHandler : IAuthorizationHandler
{
    private readonly SalonBookingDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public PermissionAuthorizationHandler(SalonBookingDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task HandleAsync(AuthorizationHandlerContext context)
    {
        foreach (var requirement in context.Requirements)
        {
            if (requirement is PermissionRequirement single && await PermitsAsync(single.Permission))
            {
                context.Succeed(requirement);
            }
            else if (requirement is AnyPermissionRequirement any)
            {
                foreach (var permission in any.Permissions)
                {
                    if (await PermitsAsync(permission))
                    {
                        context.Succeed(requirement);
                        break;
                    }
                }
            }
        }
    }

    private async Task<bool> PermitsAsync(string permission)
    {
        if (_currentUser.UserId is not > 0)
        {
            return false;
        }

        var user = await _context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.UserId == _currentUser.UserId && !item.IsDeleted)
            .Select(item => new { item.IsActive, item.UserCategory, item.TenantId, item.IsPlatformAdministrator })
            .FirstOrDefaultAsync();

        if (user is not { IsActive: true })
        {
            return false;
        }

        if (permission == PermissionCatalog.PlatformManage)
        {
            return user.IsPlatformAdministrator;
        }

        if (user.IsPlatformAdministrator && PermissionCatalog.IsStaffCode(permission))
        {
            return true;
        }

        if (user.UserCategory == UserCategory.Customer)
        {
            return PermissionCatalog.CustomerCodes.Contains(permission);
        }

        return await (
            from userRole in _context.UserRoles.AsNoTracking()
            join role in _context.Roles.IgnoreQueryFilters().AsNoTracking()
                on userRole.RoleId equals role.RoleId
            join rolePermission in _context.RolePermissions.AsNoTracking()
                on role.RoleId equals rolePermission.RoleId
            join permissionRow in _context.Permissions.AsNoTracking()
                on rolePermission.PermissionId equals permissionRow.PermissionId
            where userRole.UserId == _currentUser.UserId &&
                  role.TenantId == user.TenantId &&
                  role.IsActive &&
                  !role.IsDeleted &&
                  permissionRow.IsActive &&
                  !permissionRow.IsDeleted &&
                  permissionRow.PermissionCode == permission
            select permissionRow.PermissionId
        ).AnyAsync();
    }
}
