
using SalonBooking.Domain.Common;

namespace SalonBooking.Domain.Entities;

public class Service : TenantEntity
{
    public long ServiceId { get; set; }

    public long BranchId { get; set; }

    public long ServiceCategoryId { get; set; }

    public string ServiceCode { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Duration in minutes
    /// </summary>
    public int DurationMinutes { get; set; }

    public decimal Price { get; set; }

    /// <summary>
    /// Internal cost (optional)
    /// </summary>
    public decimal Cost { get; set; }

    /// <summary>
    /// Employee commission percentage
    /// </summary>
    public decimal CommissionPercentage { get; set; }

    public string? PhotoPath { get; set; }

    public bool IsCustomerVisible { get; set; }

    public bool IsPopular { get; set; }

    // Navigation Properties
    public Branch Branch { get; set; } = null!;

    public ServiceCategory ServiceCategory { get; set; } = null!;
}
