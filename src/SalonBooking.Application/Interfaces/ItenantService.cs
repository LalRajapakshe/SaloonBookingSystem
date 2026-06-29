using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Tenant.DTOs;

using SalonBooking.Application.Features.Tenant;
using SalonBooking.Domain.Entities;


namespace SalonBooking.Application.Interfaces;

public interface ItenantService
{
    Task<TenantResponse> CreateAsync(CreateTenantRequest request);

    Task<TenantResponse?> GetByIdAsync(long id);

    Task<PagedResult<TenantResponse>> GetTenantsAsync(TenantQueryRequest request);

    Task<TenantResponse> GetAllAsync(int page, int pageSize);

    Task<TenantResponse?> UpdateAsync(long id, UpdateTenantRequest request);

    Task<bool> DeleteAsync(long id);
}