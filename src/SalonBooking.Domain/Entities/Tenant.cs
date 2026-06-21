using SalonBooking.Domain.Common;
namespace SalonBooking.Domain.Entities;

public class Tenant : AuditableEntity
{
    public long TenantId { get; set; }

    public string TenantCode { get; set; } = string.Empty;

    public string TenantName { get; set; } = string.Empty;

    public string? ContactPerson { get; set; }

    public string? ContactNo { get; set; }

    public string? Email { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public ICollection<Branch> Branches { get; set; }
        = new List<Branch>();
}