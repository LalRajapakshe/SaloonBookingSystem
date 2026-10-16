namespace SalonBooking.Application.Features.Authentication.DTOs;

public sealed class CurrentUserResponse
{
    public long UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string UserCategory { get; set; } = string.Empty;

    public long TenantId { get; set; }

    public long? BranchId { get; set; }

    public long? EmployeeId { get; set; }

    public long? CustomerId { get; set; }

    public bool IsActive { get; set; }

    public List<string> Roles { get; set; } = [];

    public List<string> Permissions { get; set; } = [];
}
