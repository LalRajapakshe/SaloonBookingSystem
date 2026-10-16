using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonBooking.API.Authorization;
using SalonBooking.Application.Features.Scheduling;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Security;

namespace SalonBooking.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EmployeeCapabilitiesController : ControllerBase
{
    private readonly IEmployeeCapabilityService _capabilities;

    public EmployeeCapabilitiesController(IEmployeeCapabilityService capabilities)
    {
        _capabilities = capabilities;
    }

    [HttpGet]
    [RequiresPermission(PermissionCatalog.EmployeesRead)]
    public async Task<IActionResult> Get(
        [FromQuery] EmployeeCapabilityQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _capabilities.GetAsync(request, cancellationToken));
    }

    [HttpPost]
    [RequiresPermission(PermissionCatalog.EmployeesManage)]
    public async Task<IActionResult> Assign(
        AssignEmployeeCapabilityRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _capabilities.AssignAsync(request, cancellationToken));
    }

    [HttpPut("{id:long}")]
    [RequiresPermission(PermissionCatalog.EmployeesManage)]
    public async Task<IActionResult> Update(
        long id,
        UpdateEmployeeCapabilityRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _capabilities.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:long}")]
    [RequiresPermission(PermissionCatalog.EmployeesManage)]
    public async Task<IActionResult> Remove(long id, CancellationToken cancellationToken)
    {
        await _capabilities.RemoveAsync(id, cancellationToken);
        return NoContent();
    }
}
