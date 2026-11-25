using SalonBooking.Domain.Common;

namespace SalonBooking.Domain.Entities;

public class RetailProduct : TenantEntity
{
    public long RetailProductId { get; set; }

    public long BranchId { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal SellingPrice { get; set; }

    public Branch Branch { get; set; } = null!;
}
