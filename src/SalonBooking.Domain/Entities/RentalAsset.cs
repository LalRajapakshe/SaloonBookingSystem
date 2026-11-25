using SalonBooking.Domain.Common;

namespace SalonBooking.Domain.Entities;

/// <summary>
/// One reusable physical asset. This is not a retail product and has no stock quantity.
/// </summary>
public class RentalAsset : TenantEntity
{
    public long RentalAssetId { get; set; }

    public long BranchId { get; set; }

    public string AssetCode { get; set; } = string.Empty;

    public string AssetName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Category { get; set; } = string.Empty;

    public decimal ChargePerUse { get; set; }

    public int CleaningBufferMinutes { get; set; }

    public bool IsUnderMaintenance { get; set; }

    public string? ConditionNotes { get; set; }

    public string? PhotoPath { get; set; }

    public Branch Branch { get; set; } = null!;
}
