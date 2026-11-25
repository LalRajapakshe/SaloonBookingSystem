using SalonBooking.Domain.Common;

namespace SalonBooking.Domain.Entities;

/// <summary>
/// Branch tax rate. Nothing is charged when a branch has no row.
/// </summary>
public class TaxSetting : TenantEntity
{
    public long TaxSettingId { get; set; }

    public long BranchId { get; set; }

    public string Name { get; set; } = "Tax";

    public decimal RatePercent { get; set; }

    public Branch Branch { get; set; } = null!;
}
