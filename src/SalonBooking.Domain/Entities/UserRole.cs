using SalonBooking.Domain.Common;

namespace SalonBooking.Domain.Entities;

public class UserRole : AuditableEntity
{
    public long UserRoleId { get; set; }

    public long UserId { get; set; }

    public long RoleId { get; set; }

    public User User { get; set; } = null!;

    public Role Role { get; set; } = null!;
}