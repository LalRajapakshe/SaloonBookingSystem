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
