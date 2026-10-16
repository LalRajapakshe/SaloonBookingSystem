namespace SalonBooking.Application.Features.Identity;

public sealed class UserQueryRequest
{
    public string? Search { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

public sealed class SaveUserRequest
{
    public string Username { get; set; } = string.Empty;

    public string? Password { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? MobileNo { get; set; }

    public string UserCategory { get; set; } = string.Empty;

    public long? EmployeeId { get; set; }

    public long? CustomerId { get; set; }

    public long? BranchId { get; set; }

    public List<long> RoleIds { get; set; } = [];
}

public sealed class SetUserActiveRequest
{
    public bool IsActive { get; set; }
}

public sealed class SetUserRolesRequest
{
    public List<long> RoleIds { get; set; } = [];
}

public sealed class UserResponse
{
    public long UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? MobileNo { get; set; }

    public string UserCategory { get; set; } = string.Empty;

    public long TenantId { get; set; }

    public long? BranchId { get; set; }

    public long? EmployeeId { get; set; }

    public string? EmployeeName { get; set; }

    public long? CustomerId { get; set; }

    public string? CustomerName { get; set; }

    public bool IsActive { get; set; }

    public List<RoleSummaryResponse> Roles { get; set; } = [];
}

public sealed class RoleSummaryResponse
{
    public long RoleId { get; set; }

    public string RoleCode { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;
}

public sealed class SaveRoleRequest
{
    public string RoleCode { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class SetRolePermissionsRequest
{
    public List<long> PermissionIds { get; set; } = [];
}

public sealed class RoleResponse
{
    public long RoleId { get; set; }

    public string RoleCode { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public List<long> PermissionIds { get; set; } = [];
}

public sealed class PermissionResponse
{
    public long PermissionId { get; set; }

    public string PermissionCode { get; set; } = string.Empty;

    public string PermissionName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Area { get; set; } = string.Empty;
}
