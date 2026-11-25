using SalonBooking.Domain.Common;
using SalonBooking.Domain.Enums;

namespace SalonBooking.Domain.Entities;

public class BillPayment : TenantEntity
{
    public long BillPaymentId { get; set; }

    public long BranchId { get; set; }

    public long BillId { get; set; }

    public PaymentKind Kind { get; set; }

    public PaymentMethod Method { get; set; }

    /// <summary>
    /// Positive for a payment and negative for a refund.
    /// </summary>
    public decimal Amount { get; set; }

    public DateTime RecordedAt { get; set; }

    public long? RecordedByUserId { get; set; }

    public string? Notes { get; set; }

    public Bill Bill { get; set; } = null!;
}
