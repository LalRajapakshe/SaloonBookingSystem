namespace SalonBooking.Domain.Common;

public abstract class AuditableEntity : BaseEntity
{
    public DateTime CreatedDate { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? ModifiedDate { get; set; }

    public long? ModifiedBy { get; set; }

     public bool IsDeleted { get; set; }

     public bool IsActive { get; set; }
}