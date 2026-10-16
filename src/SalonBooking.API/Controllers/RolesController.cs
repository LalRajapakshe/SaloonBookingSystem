using Microsoft.AspNetCore.Mvc;
using SalonBooking.API.Authorization;
using SalonBooking.Application.Features.Identity;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Security;

namespace SalonBooking.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly IRoleAdminService _roles;

    public RolesController(IRoleAdminService roles)
    {
        _roles = roles;
    }

    [HttpGet]
    [RequiresPermission(PermissionCatalog.RolesRead)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        return Ok(await _roles.GetRolesAsync(cancellationToken));
    }

    [HttpGet("{id:long}")]
    [RequiresPermission(PermissionCatalog.RolesRead)]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        return Ok(await _roles.GetRoleAsync(id, cancellationToken));
    }

    [HttpPost]
    [RequiresPermission(PermissionCatalog.RolesManage)]
    public async Task<IActionResult> Create(SaveRoleRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _roles.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:long}")]
    [RequiresPermission(PermissionCatalog.RolesManage)]
    public async Task<IActionResult> Update(long id, SaveRoleRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _roles.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPut("{id:long}/permissions")]
    [RequiresPermission(PermissionCatalog.RolesManage)]
    public async Task<IActionResult> SetPermissions(
        long id,
        SetRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _roles.SetPermissionsAsync(id, request.PermissionIds, cancellationToken));
    }
}
