using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Common;
using SalonBooking.Application.Security;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Persistence.Context;

namespace SalonBooking.Infrastructure.Security;

internal static class SalonAdministratorGuard
{
    public static async Task EnsureRemainsAsync(
        SalonBookingDbContext context,
        long tenantId,
        long userId,
        bool userRemainsSalonAdministrator,
        CancellationToken cancellationToken)
    {
        var others = await ActiveAdministratorIdsAsync(context, tenantId, cancellationToken);
        others.Remove(userId);
        if (userRemainsSalonAdministrator)
        {
            others.Add(userId);
        }

        if (others.Count == 0)
        {
            throw new IdentityException(
                "Assign another salon administrator before deactivating the only active salon administrator.");
        }
    }

    public static bool WillRemain(User user, IEnumerable<Role> roles)
    {
        if (!user.IsActive || user.IsDeleted || user.UserCategory == UserCategory.Customer)
        {
            return false;
        }

        return roles.Any(role =>
            string.Equals(role.RoleCode, PermissionCatalog.AdminRoleCode, StringComparison.OrdinalIgnoreCase) &&
            role.TenantId == user.TenantId &&
            role.IsActive &&
            !role.IsDeleted);
    }

    public static async Task EnsureRoleDeactivationLeavesAnAdministratorAsync(
        SalonBookingDbContext context,
        Role role,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(role.RoleCode, PermissionCatalog.AdminRoleCode, StringComparison.OrdinalIgnoreCase) ||
            role.IsActive)
        {
            return;
        }

        var holders = await ActiveAdministratorIdsAsync(context, role.TenantId, cancellationToken);
        if (holders.Count > 0)
        {
            throw new IdentityException(
                "Assign another salon administrator before deactivating the only active salon administrator.");
        }
    }

    private static async Task<HashSet<long>> ActiveAdministratorIdsAsync(
        SalonBookingDbContext context,
        long tenantId,
        CancellationToken cancellationToken)
    {
        var ids = await (
            from user in context.Users.IgnoreQueryFilters().AsNoTracking()
            join link in context.UserRoles.AsNoTracking() on user.UserId equals link.UserId
            join role in context.Roles.IgnoreQueryFilters().AsNoTracking() on link.RoleId equals role.RoleId
            where user.TenantId == tenantId &&
                  user.IsActive &&
                  !user.IsDeleted &&
                  user.UserCategory != UserCategory.Customer &&
                  role.TenantId == tenantId &&
                  role.RoleCode == PermissionCatalog.AdminRoleCode &&
                  role.IsActive &&
                  !role.IsDeleted
            select user.UserId
        ).Distinct().ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }
}
