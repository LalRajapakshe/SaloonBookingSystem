using SalonBooking.Domain.Common;

namespace SalonBooking.Domain.Entities;

public class ServiceCategory : TenantEntity
{
    public long ServiceCategoryId { get; set; }

    public string CategoryCode { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    // Navigation Property
    public ICollection<Service> Services { get; set; }
        = new List<Service>();
}