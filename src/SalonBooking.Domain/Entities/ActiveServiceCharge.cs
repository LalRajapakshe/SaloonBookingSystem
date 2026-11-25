namespace SalonBooking.Domain.Entities;

/// <summary>
/// One live claim for a service line. Voiding the bill removes the row.
/// The bill line, payments, and refunds stay.
/// </summary>
public class ActiveServiceCharge
{
    public long AppointmentServiceId { get; set; }

    public long BillId { get; set; }

    public long BillLineId { get; set; }

    public long TenantId { get; set; }

    public long BranchId { get; set; }

    public AppointmentService AppointmentService { get; set; } = null!;

    public Bill Bill { get; set; } = null!;

    public BillLine BillLine { get; set; } = null!;
}
