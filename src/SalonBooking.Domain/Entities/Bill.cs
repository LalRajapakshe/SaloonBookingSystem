using SalonBooking.Domain.Common;
using SalonBooking.Domain.Enums;

namespace SalonBooking.Domain.Entities;

public class Bill : TenantEntity
{
    public long BillId { get; set; }

    public long BranchId { get; set; }

    public long CustomerId { get; set; }

    public long? AppointmentId { get; set; }

    public string? BillNumber { get; set; }

    public BillStatus Status { get; set; } = BillStatus.Draft;

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

    public DateTime BillDate { get; set; }

    public decimal Subtotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal Total { get; set; }

    public string? Notes { get; set; }

    public DateTime? FinalizedAt { get; set; }

    public DateTime? VoidedAt { get; set; }

    public long? VoidedByUserId { get; set; }

    public string? VoidReason { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public Customer Customer { get; set; } = null!;

    public Branch Branch { get; set; } = null!;

    public Appointment? Appointment { get; set; }

    public ICollection<BillLine> Lines { get; set; } = new List<BillLine>();

    public ICollection<BillPayment> Payments { get; set; } = new List<BillPayment>();
}
