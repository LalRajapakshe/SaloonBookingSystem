namespace SalonBooking.Application.Features.Rentals;

public sealed class SaveRentalAssetRequest
{
    public long? RentalAssetId { get; set; }
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
    public bool IsActive { get; set; } = true;
}

public sealed class RentalAssetResponse
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
    public bool IsActive { get; set; }
    public string? ConditionNotes { get; set; }
    public string? PhotoPath { get; set; }
    public string Availability { get; set; } = "Available";
}

public sealed class CreateRentalReservationRequest
{
    public long CustomerId { get; set; }
    public long RentalAssetId { get; set; }
    public long? AppointmentId { get; set; }
    public DateTime PlannedIssueAt { get; set; }
    public DateTime PlannedReturnAt { get; set; }
    public string? Notes { get; set; }
}

public sealed class RentalMovementRequest
{
    public string Condition { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public sealed class RentalReservationResponse
{
    public long RentalReservationId { get; set; }
    public long BranchId { get; set; }
    public long CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public long RentalAssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetCode { get; set; } = string.Empty;
    public long? AppointmentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsOverdue { get; set; }
    public DateTime PlannedIssueAt { get; set; }
    public DateTime PlannedReturnAt { get; set; }
    public DateTime? ActualIssuedAt { get; set; }
    public DateTime? ActualReturnedAt { get; set; }
    public string? IssueCondition { get; set; }
    public string? ReturnCondition { get; set; }
    public bool ConditionDiscrepancy { get; set; }
    public decimal ChargePerUse { get; set; }
    public string? Notes { get; set; }
    public bool IsBillable { get; set; }
}
