using SalonBooking.Domain.Common;

namespace SalonBooking.Domain.Entities;

public class Permission : AuditableEntity
{
    public long PermissionId { get; set; }

    public string PermissionCode { get; set; } = string.Empty;

    public string PermissionName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }
}