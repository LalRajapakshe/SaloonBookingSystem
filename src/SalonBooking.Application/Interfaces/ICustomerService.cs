using SalonBooking.Application.Features.Customer.DTOs;
using SalonBooking.Application.Features.Customer;

namespace SalonBooking.Application.Interfaces
{
public interface ICustomerService
{
    Task<CustomerResponse> CreateAsync(
        CreateCustomerRequest request);

    Task<List<CustomerResponse>> GetAllAsync();

    Task<CustomerResponse?> GetByIdAsync(
        long customerId);

    Task<CustomerResponse> UpdateAsync(
        long customerId,
        UpdateCustomerRequest request);

    Task DeleteAsync(
        long customerId);
}
}