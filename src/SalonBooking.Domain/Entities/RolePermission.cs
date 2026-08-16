using SalonBooking.Domain.Common;

namespace SalonBooking.Domain.Entities;

public class RolePermission 
{
    public long RolePermissionId { get; set; }

    public long RoleId { get; set; }

    public long PermissionId { get; set; }

    public Role Role { get; set; } = null!;

    public Permission Permission { get; set; } = null!;
}