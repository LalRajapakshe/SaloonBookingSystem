using SalonBooking.Domain.Common;
using SalonBooking.Domain.Enums;

namespace SalonBooking.Domain.Entities;

public class RentalReservation : TenantEntity
{
    public long RentalReservationId { get; set; }

    public long BranchId { get; set; }

    public long CustomerId { get; set; }

    public long RentalAssetId { get; set; }

    public long? AppointmentId { get; set; }

    public RentalReservationStatus Status { get; set; } = RentalReservationStatus.Reserved;

    public DateTime PlannedIssueAt { get; set; }

    public DateTime PlannedReturnAt { get; set; }

    public DateTime? ActualIssuedAt { get; set; }

    public DateTime? ActualReturnedAt { get; set; }

    public long? IssuedByUserId { get; set; }

    public long? ReturnedByUserId { get; set; }

    public string? IssueCondition { get; set; }

    public string? ReturnCondition { get; set; }

    public bool ConditionDiscrepancy { get; set; }

    public decimal ChargePerUse { get; set; }

    public string? Notes { get; set; }

    public Customer Customer { get; set; } = null!;

    public RentalAsset Asset { get; set; } = null!;

    public Appointment? Appointment { get; set; }
}
