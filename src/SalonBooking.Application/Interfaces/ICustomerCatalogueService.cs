using SalonBooking.Application.Common;
using SalonBooking.Application.Features.CustomerCatalogue;

namespace SalonBooking.Application.Interfaces;

public interface ICustomerCatalogueService
{
    Task<IReadOnlyList<CustomerBranchResponse>> ListBranchesAsync(CancellationToken cancellationToken = default);

    Task<CustomerBranchResponse> GetBranchAsync(long branchId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerServiceResponse>> ListServicesAsync(long branchId, CancellationToken cancellationToken = default);

    Task<CustomerServiceResponse> GetServiceAsync(long branchId, long serviceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerStylistResponse>> ListStylistsAsync(
        long branchId,
        long serviceId,
        CancellationToken cancellationToken = default);

    Task<CustomerStylistResponse> GetStylistAsync(long employeeId, CancellationToken cancellationToken = default);

    Task<CustomerAvailabilityResponse> GetAvailabilityAsync(
        long branchId,
        long serviceId,
        DateOnly date,
        long? employeeId,
        CancellationToken cancellationToken = default);

    Task<StoredFileContent?> OpenBranchPhotoAsync(long branchId, CancellationToken cancellationToken = default);

    Task<StoredFileContent?> OpenServicePhotoAsync(long serviceId, CancellationToken cancellationToken = default);

    Task<StoredFileContent?> OpenStylistPhotoAsync(long employeeId, CancellationToken cancellationToken = default);
}
