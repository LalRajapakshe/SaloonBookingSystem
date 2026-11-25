namespace SalonBooking.Domain.Entities;

/// <summary>
/// One live claim for a returned rental. Voiding the bill removes the row.
/// </summary>
public class ActiveRentalCharge
{
    public long RentalReservationId { get; set; }

    public long BillId { get; set; }

    public long BillLineId { get; set; }

    public long TenantId { get; set; }

    public long BranchId { get; set; }

    public RentalReservation Reservation { get; set; } = null!;

    public Bill Bill { get; set; } = null!;

    public BillLine BillLine { get; set; } = null!;
}
