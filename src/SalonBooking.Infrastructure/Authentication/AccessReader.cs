using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Authentication.DTOs;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Security;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Infrastructure.Security;
using SalonBooking.Persistence.Context;

namespace SalonBooking.Infrastructure.Authentication;

internal static class AccessReader
{
    public static async Task<CurrentUserResponse> ReadProfileAsync(
        SalonBookingDbContext context,
        User user,
        CancellationToken cancellationToken)
    {
        var access = await ReadAccessAsync(context, user.UserId, user.TenantId, cancellationToken);
        return new CurrentUserResponse
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            UserCategory = user.UserCategory.ToString(),
            TenantId = user.TenantId,
            BranchId = user.BranchId,
            EmployeeId = user.EmployeeId,
            CustomerId = user.CustomerId,
            IsActive = user.IsActive,
            Roles = access.RoleNames,
            Permissions = EffectivePermissions(user.UserCategory, access.PermissionCodes)
        };
    }

    public static async Task EnsureMayGrantAsync(
        SalonBookingDbContext context,
        ICurrentUserService currentUser,
        IEnumerable<string> permissionCodes,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not long callerId)
        {
            throw new UnauthorizedAccessException("Sign in is required.");
        }

        var access = await ReadAccessAsync(context, callerId, currentUser.RequireTenantId(), cancellationToken);
        var held = access.PermissionCodes.ToHashSet(StringComparer.Ordinal);
        if (permissionCodes.Any(code => PermissionCatalog.IsStaffCode(code) && !held.Contains(code)))
        {
            throw new IdentityException("You cannot grant a permission that you do not have.");
        }
    }

    public static List<string> EffectivePermissions(UserCategory category, IReadOnlyList<string> permissionCodes)
    {
        if (category != UserCategory.Customer)
        {
            return permissionCodes.ToList();
        }

        return permissionCodes.Where(code => !PermissionCatalog.IsStaffCode(code)).ToList();
    }

    public static async Task<UserAccess> ReadAccessAsync(
        SalonBookingDbContext context,
        long userId,
        long tenantId,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from userRole in context.UserRoles.AsNoTracking()
            join role in context.Roles.IgnoreQueryFilters().AsNoTracking()
                on userRole.RoleId equals role.RoleId
            where userRole.UserId == userId &&
                  role.TenantId == tenantId &&
                  role.IsActive &&
                  !role.IsDeleted
            select new { role.RoleId, role.RoleName }
        ).Distinct().ToListAsync(cancellationToken);

        var roleIds = rows.Select(row => row.RoleId).ToList();
        var permissions = roleIds.Count == 0
            ? []
            : await (
                from rolePermission in context.RolePermissions.AsNoTracking()
                join permission in context.Permissions.AsNoTracking()
                    on rolePermission.PermissionId equals permission.PermissionId
                where roleIds.Contains(rolePermission.RoleId) &&
                      permission.IsActive &&
                      !permission.IsDeleted
                select permission.PermissionCode
            ).Distinct().ToListAsync(cancellationToken);

        return new UserAccess(
            rows.Select(row => row.RoleName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name).ToList(),
            permissions.OrderBy(code => code, StringComparer.Ordinal).ToList());
    }

    public static async Task<HashSet<long>> ManagerIdsAsync(
        SalonBookingDbContext context,
        long tenantId,
        CancellationToken cancellationToken)
    {
        var managers = await (
            from user in context.Users.IgnoreQueryFilters().AsNoTracking()
            join userRole in context.UserRoles.AsNoTracking() on user.UserId equals userRole.UserId
            join role in context.Roles.IgnoreQueryFilters().AsNoTracking() on userRole.RoleId equals role.RoleId
            join rolePermission in context.RolePermissions.AsNoTracking() on role.RoleId equals rolePermission.RoleId
            join permission in context.Permissions.AsNoTracking() on rolePermission.PermissionId equals permission.PermissionId
            where user.TenantId == tenantId &&
                  user.IsActive &&
                  !user.IsDeleted &&
                  user.UserCategory != UserCategory.Customer &&
                  role.TenantId == tenantId &&
                  role.IsActive &&
                  !role.IsDeleted &&
                  permission.PermissionCode == PermissionCatalog.UsersManage &&
                  permission.IsActive &&
                  !permission.IsDeleted
            select user.UserId
        ).Distinct().ToListAsync(cancellationToken);

        return managers.ToHashSet();
    }
}

internal sealed record UserAccess(
    List<string> RoleNames,
    List<string> PermissionCodes);
