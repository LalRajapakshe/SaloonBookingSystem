using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonBooking.API.Authorization;
using SalonBooking.Application.Features.Billing;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Security;

namespace SalonBooking.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TaxSettingsController : ControllerBase
{
    private readonly IBillingService _billing;

    public TaxSettingsController(IBillingService billing)
    {
        _billing = billing;
    }

    [HttpGet]
    [RequiresPermission(PermissionCatalog.TaxRead)]
    public async Task<IActionResult> Get([FromQuery] long branchId, CancellationToken cancellationToken)
    {
        return Ok(await _billing.GetTaxAsync(branchId, cancellationToken));
    }

    [HttpPut]
    [RequiresPermission(PermissionCatalog.TaxManage)]
    public async Task<IActionResult> Save(SaveTaxSettingRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _billing.SaveTaxAsync(request, cancellationToken));
    }
}
