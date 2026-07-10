namespace SalonBooking.Domain.Common;

public abstract class TenantEntity : AuditableEntity
{
    public long TenantId { get; set; }



   
}