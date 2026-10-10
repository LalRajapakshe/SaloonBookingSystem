using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Features.Tenant.DTOs;

namespace SalonBooking.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TenantController : ControllerBase
{
    private readonly ItenantService _tenantService;

    public TenantController(ItenantService tenantService)
    {
        _tenantService = tenantService;
    }

    // Tenant provisioning stays closed until a platform administrator
    // exists outside this tenant-scoped API.
    [HttpPost]
    public async Task<IActionResult> Create(CreateTenantRequest request)
    {
        var result = await _tenantService.CreateAsync(request);

        return Ok(result);
    }
    [HttpGet]
    public async Task<IActionResult> GetTenants(
        [FromQuery] TenantQueryRequest request)
    {
        var result = await _tenantService.GetTenantsAsync(request);

        return Ok(result);
    }

        [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var tenant = await _tenantService.GetByIdAsync(id);

        if (tenant == null)
            return NotFound();

        return Ok(tenant);
    }

     [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(
        long id,
        UpdateTenantRequest request)
    {
        var tenant =
            await _tenantService.UpdateAsync(id, request);

        if (tenant == null)
            return NotFound();    

        return Ok(tenant);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _tenantService.DeleteAsync(id);

        return NoContent();
    }
}