using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Billing;

namespace SalonBooking.Application.Interfaces;

public interface IBillingService
{
    Task<PagedResult<RetailProductResponse>> ListProductsAsync(RetailProductQuery query, CancellationToken cancellationToken = default);

    Task<RetailProductResponse> CreateProductAsync(SaveRetailProductRequest request, CancellationToken cancellationToken = default);

    Task<RetailProductResponse> UpdateProductAsync(long retailProductId, SaveRetailProductRequest request, CancellationToken cancellationToken = default);

    Task<TaxSettingResponse> GetTaxAsync(long branchId, CancellationToken cancellationToken = default);

    Task<TaxSettingResponse> SaveTaxAsync(SaveTaxSettingRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<BillResponse>> ListBillsAsync(BillQuery query, CancellationToken cancellationToken = default);

    Task<BillResponse> GetBillAsync(long billId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EligibleServiceLineResponse>> ListEligibleServicesAsync(long appointmentId, CancellationToken cancellationToken = default);

    Task<BillingCoverageResponse> GetCoverageAsync(long appointmentId, CancellationToken cancellationToken = default);

    Task<BillResponse> CreateBillAsync(CreateBillRequest request, CancellationToken cancellationToken = default);

    Task<BillResponse> AddServiceLinesAsync(long billId, AddServiceLinesRequest request, CancellationToken cancellationToken = default);

    Task<BillResponse> AddProductLineAsync(long billId, AddProductLineRequest request, CancellationToken cancellationToken = default);

    Task<BillResponse> AddRentalLineAsync(long billId, AddRentalLineRequest request, CancellationToken cancellationToken = default);

    Task<BillResponse> UpdateProductLineAsync(long billId, long billLineId, UpdateProductLineRequest request, CancellationToken cancellationToken = default);

    Task<BillResponse> RemoveLineAsync(long billId, long billLineId, string rowVersion, CancellationToken cancellationToken = default);

    Task<BillResponse> FinalizeAsync(long billId, string? rowVersion, CancellationToken cancellationToken = default);

    Task<BillResponse> RecordPaymentAsync(long billId, RecordPaymentRequest request, CancellationToken cancellationToken = default);

    Task<BillResponse> RecordRefundAsync(long billId, RecordPaymentRequest request, CancellationToken cancellationToken = default);

    Task<BillResponse> VoidAsync(long billId, VoidBillRequest request, CancellationToken cancellationToken = default);
}
