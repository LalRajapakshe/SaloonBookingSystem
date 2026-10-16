using SalonBooking.Application.Features.Identity;

namespace SalonBooking.Application.Interfaces;

public interface IRoleAdminService
{
    Task<IReadOnlyList<RoleResponse>> GetRolesAsync(
        CancellationToken cancellationToken = default);

    Task<RoleResponse> GetRoleAsync(
        long roleId,
        CancellationToken cancellationToken = default);

    Task<RoleResponse> CreateAsync(
        SaveRoleRequest request,
        CancellationToken cancellationToken = default);

    Task<RoleResponse> UpdateAsync(
        long roleId,
        SaveRoleRequest request,
        CancellationToken cancellationToken = default);

    Task<RoleResponse> SetPermissionsAsync(
        long roleId,
        IReadOnlyCollection<long> permissionIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermissionResponse>> GetPermissionCatalogAsync(
        CancellationToken cancellationToken = default);
}