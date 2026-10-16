using SalonBooking.Domain.Common;
using SalonBooking.Domain.Enums;

namespace SalonBooking.Domain.Entities;

public class User : TenantEntity
{
    public long UserId { get; set; }

    public long? BranchId { get; set; }

    public UserCategory UserCategory { get; set; } = UserCategory.General;

    public long? EmployeeId { get; set; }

    public long? CustomerId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? MobileNo { get; set; }

    public DateTime? LastLoginDate { get; set; }

    public Branch? Branch { get; set; }

    public Employee? Employee { get; set; }

    public Customer? Customer { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    /// <summary>
    /// Legacy label kept so existing rows can be mapped onto roles.
    /// Authorization uses role and permission rows, not this value.
    /// </summary>
    public string Role { get; set; } = string.Empty;
}