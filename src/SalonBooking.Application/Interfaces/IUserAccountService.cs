using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Identity;

namespace SalonBooking.Application.Interfaces;

public interface IUserAccountService
{
    Task<PagedResult<UserResponse>> GetUsersAsync(
        UserQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<UserResponse> GetUserAsync(
        long userId,
        CancellationToken cancellationToken = default);

    Task<UserResponse> CreateAsync(
        SaveUserRequest request,
        CancellationToken cancellationToken = default);

    Task<UserResponse> UpdateAsync(
        long userId,
        SaveUserRequest request,
        CancellationToken cancellationToken = default);

    Task<UserResponse> SetActiveAsync(
        long userId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<UserResponse> SetRolesAsync(
        long userId,
        IReadOnlyCollection<long> roleIds,
        CancellationToken cancellationToken = default);
}
