using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Customer.DTOs;

namespace SalonBooking.Application.Interfaces;

public interface ICustomerPhotoService
{
    Task<CustomerResponse> SaveAsync(long customerId, byte[] content, CancellationToken cancellationToken = default);

    Task<CustomerPhotoContent?> OpenAsync(long customerId, CancellationToken cancellationToken = default);

    Task<CustomerResponse> RemoveAsync(long customerId, CancellationToken cancellationToken = default);
}
