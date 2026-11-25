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
public class RetailProductsController : ControllerBase
{
    private readonly IBillingService _billing;

    public RetailProductsController(IBillingService billing)
    {
        _billing = billing;
    }

    [HttpGet]
    [RequiresPermission(PermissionCatalog.ProductsRead)]
    public async Task<IActionResult> List([FromQuery] RetailProductQuery query, CancellationToken cancellationToken)
    {
        return Ok(await _billing.ListProductsAsync(query, cancellationToken));
    }

    [HttpPost]
    [RequiresPermission(PermissionCatalog.ProductsManage)]
    public async Task<IActionResult> Create(SaveRetailProductRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _billing.CreateProductAsync(request, cancellationToken));
    }

    [HttpPut("{id:long}")]
    [RequiresPermission(PermissionCatalog.ProductsManage)]
    public async Task<IActionResult> Update(long id, SaveRetailProductRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _billing.UpdateProductAsync(id, request, cancellationToken));
    }
}
