using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Interfaces;

namespace SalonBooking.Infrastructure.Security;

public static class TenantBranchScope
{
    public static void EnsureCanAccessBranch(
        this ICurrentUserService user,
        long branchId)
    {
        if (user.BranchId is > 0 &&
            user.BranchId.Value != branchId)
        {
            throw new UnauthorizedAccessException(
                "You cannot access another branch.");
        }
    }

    public static async Task<TEntity> RequireOwnedAsync<TEntity>(
        this IQueryable<TEntity> query,
        ICurrentUserService user,
        Expression<Func<TEntity, bool>> predicate,
        Expression<Func<TEntity, long>> branchIdSelector,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var entity = await query
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(predicate, cancellationToken);

        if (entity == null)
        {
            throw new KeyNotFoundException();
        }

        user.EnsureCanAccessBranch(branchIdSelector.Compile()(entity));
        return entity;
    }

    public static long RequireTenantId(this ICurrentUserService user)
    {
        if (user.TenantId is not > 0)
        {
            throw new UnauthorizedAccessException(
                "Tenant claim is missing.");
        }

        return user.TenantId.Value;
    }

    public static long ResolveBranchId(
        this ICurrentUserService user,
        long requestedBranchId = 0)
    {
        if (user.BranchId is > 0)
        {
            if (requestedBranchId > 0 &&
                requestedBranchId != user.BranchId.Value)
            {
                throw new UnauthorizedAccessException(
                    "You cannot access another branch.");
            }

            return user.BranchId.Value;
        }

        if (requestedBranchId > 0)
        {
            return requestedBranchId;
        }

        throw new InvalidOperationException(
            "A branch is required.");
    }
}
