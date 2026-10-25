using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Security;
using SalonBooking.Domain.Enums;
using SalonBooking.Persistence.Context;

namespace SalonBooking.API.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly SalonBookingDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public PermissionAuthorizationHandler(SalonBookingDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (_currentUser.UserId is not > 0)
        {
            return;
        }

        var user = await _context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.UserId == _currentUser.UserId && !item.IsDeleted)
            .Select(item => new { item.IsActive, item.UserCategory, item.TenantId })
            .FirstOrDefaultAsync();

        if (user is not { IsActive: true })
        {
            return;
        }

        if (user.UserCategory == UserCategory.Customer)
        {
            if (PermissionCatalog.CustomerCodes.Contains(requirement.Permission))
            {
                context.Succeed(requirement);
            }

            return;
        }

        var granted = await (
            from userRole in _context.UserRoles.AsNoTracking()
            join role in _context.Roles.IgnoreQueryFilters().AsNoTracking()
                on userRole.RoleId equals role.RoleId
            join rolePermission in _context.RolePermissions.AsNoTracking()
                on role.RoleId equals rolePermission.RoleId
            join permission in _context.Permissions.AsNoTracking()
                on rolePermission.PermissionId equals permission.PermissionId
            where userRole.UserId == _currentUser.UserId &&
                  role.TenantId == user.TenantId &&
                  role.IsActive &&
                  !role.IsDeleted &&
                  permission.IsActive &&
                  !permission.IsDeleted &&
                  permission.PermissionCode == requirement.Permission
            select permission.PermissionId
        ).AnyAsync();

        if (granted)
        {
            context.Succeed(requirement);
        }
    }
}
