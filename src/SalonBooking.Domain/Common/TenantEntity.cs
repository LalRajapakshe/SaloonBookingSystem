namespace SalonBooking.Domain.Common;

public abstract class TenantEntity : AuditableEntity
{
    public long TenantId { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }
}