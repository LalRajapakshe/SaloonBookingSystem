using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonBooking.API.Authorization;
using SalonBooking.Application.Features.Tenant.DTOs;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Security;

namespace SalonBooking.API.Controllers;

[Authorize]
[ApiController]
[Route("api/Platform")]
public class PlatformController : ControllerBase
{
    private readonly ItenantService _tenants;

    public PlatformController(ItenantService tenants)
    {
        _tenants = tenants;
    }

    [HttpGet("tenants")]
    [RequiresPermission(PermissionCatalog.PlatformManage)]
    public async Task<IActionResult> ListTenants(
        [FromQuery] TenantQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _tenants.ListForPlatformAsync(request, cancellationToken));
    }

    [HttpPost("tenants/{tenantId:long}/salon-administrator")]
    [RequiresPermission(PermissionCatalog.PlatformManage)]
    public async Task<IActionResult> AssignSalonAdministrator(
        long tenantId,
        AssignSalonAdministratorRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _tenants.AssignSalonAdministratorAsync(
            tenantId,
            request.Username,
            request.Password,
            request.FirstName,
            request.LastName,
            request.Email,
            cancellationToken));
    }
}

public sealed class AssignSalonAdministratorRequest
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? Email { get; set; }
}
