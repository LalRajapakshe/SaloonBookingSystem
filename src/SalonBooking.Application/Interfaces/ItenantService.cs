using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Identity;
using SalonBooking.Application.Features.Tenant.DTOs;

using SalonBooking.Application.Features.Tenant;
using SalonBooking.Domain.Entities;


namespace SalonBooking.Application.Interfaces;

public interface ItenantService
{
    Task<TenantResponse> CreateAsync(CreateTenantRequest request);

    Task<UserResponse> AssignSalonAdministratorAsync(
        long tenantId,
        string username,
        string password,
        string firstName,
        string lastName,
        string? email,
        CancellationToken cancellationToken = default);

    Task<TenantResponse?> GetByIdAsync(long id);

    Task<PagedResult<TenantResponse>> GetTenantsAsync(TenantQueryRequest request);

    Task<PagedResult<TenantResponse>> ListForPlatformAsync(
        TenantQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<TenantResponse> GetAllAsync(int page, int pageSize);

    Task<TenantResponse?> UpdateAsync(long id, UpdateTenantRequest request);

    Task<bool> DeleteAsync(long id);
}