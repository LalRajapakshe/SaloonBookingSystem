using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Branch.DTOs;

using SalonBooking.Application.Features.Tenant;
using SalonBooking.Domain.Entities;


namespace SalonBooking.Application.Interfaces;

public interface IBranchService
{
    Task<BranchResponse> CreateAsync(CreateBranchRequest request);

    Task<BranchResponse?> GetByIdAsync(long id);

    Task<PagedResult<BranchResponse>> GetBranchAsync(BranchQueryRequest request);

    Task<BranchResponse> GetAllAsync(int page, int pageSize);

    Task<BranchResponse?> UpdateAsync(long id, UpdateBranchRequest request);

    Task<bool> DeleteAsync(long id);
}