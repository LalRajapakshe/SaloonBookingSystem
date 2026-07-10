using SalonBooking.Application.Features.Service.DTOs;
using SalonBooking.Application.Features.Service;
using SalonBooking.Domain.Entities;
using SalonBooking.Application.Common;

namespace SalonBooking.Application.Interfaces
{
public interface IServiceService
{
    Task<ServiceResponse> CreateAsync(
        CreateServiceRequest request);

        Task<PagedResult<ServiceResponse>> GetServicesAsync(ServiceQueryRequest request);

    Task<List<ServiceResponse>> GetAllAsync();

    Task<ServiceResponse?> GetByIdAsync(
        long serviceId);

    Task<ServiceResponse> UpdateAsync(
        long serviceId,
        UpdateServiceRequest request);

    Task DeleteAsync(
        long serviceId);
}
}