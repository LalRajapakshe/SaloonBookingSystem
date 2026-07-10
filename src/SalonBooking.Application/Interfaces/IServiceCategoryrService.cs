using SalonBooking.Application.Features.ServiceCategoryr.DTOs;
using SalonBooking.Application.Features.ServiceCategory;
using SalonBooking.Domain.Entities;
using SalonBooking.Application.Common;

namespace SalonBooking.Application.Interfaces
{
public interface IServiceCategoryService
{
    Task<ServiceCategoryResponse> CreateAsync(
        CreateServiceCategoryRequest request);

        Task<PagedResult<ServiceCategoryResponse>> GetServiceCategoriesAsync(ServiceCategoryQueryRequest request);

    Task<List<ServiceCategoryResponse>> GetAllAsync();

    Task<ServiceCategoryResponse?> GetByIdAsync(
        long serviceCategoryId);

    Task<ServiceCategoryResponse> UpdateAsync(
        long serviceCategoryId,
        UpdateServiceCategoryRequest request);

    Task DeleteAsync(
        long serviceCategoryId);
}
}