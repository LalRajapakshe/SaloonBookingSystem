using SalonBooking.Application.Interfaces;

namespace SalonBooking.Infrastructure.Security;

public static class TenantBranchScope
{
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
