using SalonBooking.Application.Features.Operations;

namespace SalonBooking.Application.Interfaces;

public interface IOperationsService
{
    Task<OperationsDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken = default);
}
