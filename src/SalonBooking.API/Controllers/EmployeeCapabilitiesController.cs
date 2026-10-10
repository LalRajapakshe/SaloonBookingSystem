using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonBooking.Application.Features.Scheduling;
using SalonBooking.Application.Interfaces;

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
    public async Task<IActionResult> Get(
        [FromQuery] EmployeeCapabilityQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _capabilities.GetAsync(request, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Assign(
        AssignEmployeeCapabilityRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _capabilities.AssignAsync(request, cancellationToken));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(
        long id,
        UpdateEmployeeCapabilityRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _capabilities.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Remove(long id, CancellationToken cancellationToken)
    {
        await _capabilities.RemoveAsync(id, cancellationToken);
        return NoContent();
    }
}
