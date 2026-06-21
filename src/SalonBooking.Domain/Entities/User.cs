using SalonBooking.Domain.Common;
namespace SalonBooking.Domain.Entities;

public class User : TenantEntity
{
    public long UserId { get; set; }

    public long? BranchId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? MobileNo { get; set; }

    public DateTime? LastLoginDate { get; set; }

    public Branch? Branch { get; set; }
}