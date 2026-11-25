using SalonBooking.Application.Features.Rentals;

namespace SalonBooking.Application.Interfaces;

public interface IRentalService
{
    Task<IReadOnlyList<RentalAssetResponse>> ListAssetsAsync(bool includeInactive, CancellationToken cancellationToken = default);
    Task<RentalAssetResponse> SaveAssetAsync(SaveRentalAssetRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RentalReservationResponse>> ListReservationsAsync(CancellationToken cancellationToken = default);
    Task<RentalReservationResponse> CreateReservationAsync(CreateRentalReservationRequest request, CancellationToken cancellationToken = default);
    Task<RentalReservationResponse> CancelReservationAsync(long id, CancellationToken cancellationToken = default);
    Task<RentalReservationResponse> IssueAsync(long id, RentalMovementRequest request, CancellationToken cancellationToken = default);
    Task<RentalReservationResponse> ReturnAsync(long id, RentalMovementRequest request, CancellationToken cancellationToken = default);
}
