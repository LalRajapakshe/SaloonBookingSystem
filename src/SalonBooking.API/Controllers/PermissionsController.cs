using Microsoft.AspNetCore.Mvc;
using SalonBooking.API.Authorization;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Security;

namespace SalonBooking.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PermissionsController : ControllerBase
{
    private readonly IRoleAdminService _roles;

    public PermissionsController(IRoleAdminService roles)
    {
        _roles = roles;
    }

    [HttpGet]
    [RequiresPermission(PermissionCatalog.PermissionsRead)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        return Ok(await _roles.GetPermissionCatalogAsync(cancellationToken));
    }
}
