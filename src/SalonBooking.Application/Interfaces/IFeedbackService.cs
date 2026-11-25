using SalonBooking.Application.Features.Feedback;

namespace SalonBooking.Application.Interfaces;

public interface IFeedbackService
{
    Task<IReadOnlyList<PendingFeedbackResponse>> ListPendingForCurrentCustomerAsync(CancellationToken cancellationToken = default);
    Task<ServiceFeedbackResponse> SubmitAsync(SubmitFeedbackRequest request, CancellationToken cancellationToken = default);
    Task EnsureCustomerMayContinueAsync(CancellationToken cancellationToken = default);
}
