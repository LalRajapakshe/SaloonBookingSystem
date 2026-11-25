using SalonBooking.Application.Common;

namespace SalonBooking.Application.Features.Billing;

public sealed class RetailProductQuery
{
    public string? Search { get; set; }

    public bool IncludeInactive { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

public sealed class SaveRetailProductRequest
{
    public long BranchId { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal SellingPrice { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class RetailProductResponse
{
    public long RetailProductId { get; set; }

    public long BranchId { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal SellingPrice { get; set; }

    public bool IsActive { get; set; }
}

public sealed class TaxSettingResponse
{
    public long BranchId { get; set; }

    public bool Configured { get; set; }

    public string Name { get; set; } = "Tax";

    public decimal RatePercent { get; set; }
}

public sealed class SaveTaxSettingRequest
{
    public long BranchId { get; set; }

    public string Name { get; set; } = "Tax";

    public decimal RatePercent { get; set; }
}

public sealed class ProductLineInput
{
    public long RetailProductId { get; set; }

    public int Quantity { get; set; } = 1;

    public decimal? UnitPrice { get; set; }
}

public sealed class CreateBillRequest
{
    public long BranchId { get; set; }

    public long CustomerId { get; set; }

    public long? AppointmentId { get; set; }

    public List<long> AppointmentServiceIds { get; set; } = [];

    public List<ProductLineInput> Products { get; set; } = [];

    public string? Notes { get; set; }
}

public sealed class AddServiceLinesRequest
{
    public string RowVersion { get; set; } = string.Empty;

    public List<long> AppointmentServiceIds { get; set; } = [];
}

public sealed class AddRentalLineRequest
{
    public string RowVersion { get; set; } = string.Empty;

    public long RentalReservationId { get; set; }
}

public sealed class AddProductLineRequest
{
    public string RowVersion { get; set; } = string.Empty;

    public long RetailProductId { get; set; }

    public int Quantity { get; set; } = 1;

    public decimal? UnitPrice { get; set; }
}

public sealed class UpdateProductLineRequest
{
    public string RowVersion { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}

public sealed class RecordPaymentRequest
{
    public string? RowVersion { get; set; }

    public string Method { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string? Notes { get; set; }
}

public sealed class RowVersionRequest
{
    public string? RowVersion { get; set; }
}

public sealed class VoidBillRequest
{
    public string? RowVersion { get; set; }

    public string Reason { get; set; } = string.Empty;
}

public sealed class BillQuery
{
    public string? Status { get; set; }

    public string? PaymentStatus { get; set; }

    public long? CustomerId { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

public sealed class BillLineResponse
{
    public long BillLineId { get; set; }

    public string LineType { get; set; } = string.Empty;

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
}

public sealed class BillPaymentResponse
{
    public long BillPaymentId { get; set; }

    public string Kind { get; set; } = string.Empty;

    public string Method { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime RecordedAt { get; set; }

    public long? RecordedByUserId { get; set; }

    public string? Notes { get; set; }
}

public sealed class BillResponse
{
    public long BillId { get; set; }

    public long BranchId { get; set; }

    public long CustomerId { get; set; }

    public string? CustomerName { get; set; }

    public long? AppointmentId { get; set; }

    public string? BillNumber { get; set; }

    public string Status { get; set; } = string.Empty;

    public string PaymentStatus { get; set; } = string.Empty;

    public DateTime BillDate { get; set; }

    public decimal Subtotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal Total { get; set; }

    public decimal NetCollected { get; set; }

    public decimal Outstanding { get; set; }

    public string? Notes { get; set; }

    public DateTime? FinalizedAt { get; set; }

    public DateTime? VoidedAt { get; set; }

    public string? VoidReason { get; set; }

    public string RowVersion { get; set; } = string.Empty;

    public List<BillLineResponse> Lines { get; set; } = [];

    public List<BillPaymentResponse> Payments { get; set; } = [];
}

public sealed class EligibleServiceLineResponse
{
    public long AppointmentServiceId { get; set; }

    public long AppointmentId { get; set; }

    public long ServiceId { get; set; }

    public string ServiceName { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public long? EmployeeId { get; set; }
}

public sealed class BillingCoverageResponse
{
    public long AppointmentId { get; set; }

    public string Coverage { get; set; } = string.Empty;

    public int BillableLineCount { get; set; }

    public int UnbilledLineCount { get; set; }

    public int DraftLineCount { get; set; }

    public int FinalizedLineCount { get; set; }
}
