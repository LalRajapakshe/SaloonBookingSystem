using Microsoft.AspNetCore.Authorization;

namespace SalonBooking.API.Authorization;

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }

    public string Permission { get; }
}

public sealed class RequiresPermissionAttribute : AuthorizeAttribute
{
    public const string Prefix = "permission:";

    public RequiresPermissionAttribute(string permission)
    {
        Policy = Prefix + permission;
    }
}

public sealed class AnyPermissionRequirement : IAuthorizationRequirement
{
    public AnyPermissionRequirement(IReadOnlyList<string> permissions)
    {
        Permissions = permissions;
    }

    public IReadOnlyList<string> Permissions { get; }
}

public sealed class RequiresAnyPermissionAttribute : AuthorizeAttribute
{
    public const string NotificationsPolicy = "permission-any:notifications";
    public const string LineProgressPolicy = "permission-any:line-progress";
    public const string LineHistoryPolicy = "permission-any:line-history";

    public RequiresAnyPermissionAttribute()
    {
        Policy = NotificationsPolicy;
    }

    public RequiresAnyPermissionAttribute(string policy)
    {
        Policy = policy;
    }
}
