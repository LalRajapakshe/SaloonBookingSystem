using SalonBooking.Domain.Common;
using SalonBooking.Domain.Enums;

namespace SalonBooking.Domain.Entities;

public class BillLine : TenantEntity
{
    public long BillLineId { get; set; }

    public long BranchId { get; set; }

    public long BillId { get; set; }

    public BillLineType LineType { get; set; }

    public string Description { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxRatePercent { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }

    public long? AppointmentServiceId { get; set; }

    public long? RetailProductId { get; set; }

    public long? RentalReservationId { get; set; }

    public Bill Bill { get; set; } = null!;

    public AppointmentService? AppointmentService { get; set; }

    public RetailProduct? RetailProduct { get; set; }

    public RentalReservation? RentalReservation { get; set; }
}
