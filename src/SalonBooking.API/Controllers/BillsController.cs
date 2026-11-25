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
public class BillsController : ControllerBase
{
    private readonly IBillingService _billing;

    public BillsController(IBillingService billing)
    {
        _billing = billing;
    }

    [HttpGet]
    [RequiresPermission(PermissionCatalog.BillsRead)]
    public async Task<IActionResult> List([FromQuery] BillQuery query, CancellationToken cancellationToken)
    {
        return Ok(await _billing.ListBillsAsync(query, cancellationToken));
    }

    [HttpGet("coverage/{appointmentId:long}")]
    [RequiresPermission(PermissionCatalog.BillsRead)]
    public async Task<IActionResult> Coverage(long appointmentId, CancellationToken cancellationToken)
    {
        return Ok(await _billing.GetCoverageAsync(appointmentId, cancellationToken));
    }

    [HttpGet("eligible-services/{appointmentId:long}")]
    [RequiresPermission(PermissionCatalog.BillsRead)]
    public async Task<IActionResult> Eligible(long appointmentId, CancellationToken cancellationToken)
    {
        return Ok(await _billing.ListEligibleServicesAsync(appointmentId, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [RequiresPermission(PermissionCatalog.BillsRead)]
    public async Task<IActionResult> Get(long id, CancellationToken cancellationToken)
    {
        return Ok(await _billing.GetBillAsync(id, cancellationToken));
    }

    [HttpPost]
    [RequiresPermission(PermissionCatalog.BillsManage)]
    public async Task<IActionResult> Create(CreateBillRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _billing.CreateBillAsync(request, cancellationToken));
    }

    [HttpPost("{id:long}/service-lines")]
    [RequiresPermission(PermissionCatalog.BillsManage)]
    public async Task<IActionResult> AddServices(long id, AddServiceLinesRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _billing.AddServiceLinesAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:long}/product-lines")]
    [RequiresPermission(PermissionCatalog.BillsManage)]
    public async Task<IActionResult> AddProduct(long id, AddProductLineRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _billing.AddProductLineAsync(id, request, cancellationToken));
    }

    [HttpPut("{id:long}/lines/{lineId:long}")]
    [RequiresPermission(PermissionCatalog.BillsManage)]
    public async Task<IActionResult> UpdateLine(
        long id,
        long lineId,
        UpdateProductLineRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _billing.UpdateProductLineAsync(id, lineId, request, cancellationToken));
    }

    [HttpDelete("{id:long}/lines/{lineId:long}")]
    [RequiresPermission(PermissionCatalog.BillsManage)]
    public async Task<IActionResult> RemoveLine(
        long id,
        long lineId,
        [FromQuery] string rowVersion,
        CancellationToken cancellationToken)
    {
        return Ok(await _billing.RemoveLineAsync(id, lineId, rowVersion, cancellationToken));
    }

    [HttpPost("{id:long}/finalize")]
    [RequiresPermission(PermissionCatalog.BillsManage)]
    public async Task<IActionResult> Finalize(long id, RowVersionRequest? request, CancellationToken cancellationToken)
    {
        return Ok(await _billing.FinalizeAsync(id, request?.RowVersion, cancellationToken));
    }

    [HttpPost("{id:long}/payments")]
    [RequiresPermission(PermissionCatalog.PaymentsCollect)]
    public async Task<IActionResult> Pay(long id, RecordPaymentRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _billing.RecordPaymentAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:long}/refunds")]
    [RequiresPermission(PermissionCatalog.PaymentsCollect)]
    public async Task<IActionResult> Refund(long id, RecordPaymentRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _billing.RecordRefundAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:long}/rentals")]
    [RequiresPermission(PermissionCatalog.BillsManage)]
    public async Task<IActionResult> AddRental(long id, AddRentalLineRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _billing.AddRentalLineAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:long}/void")]
    [RequiresPermission(PermissionCatalog.BillsManage)]
    public async Task<IActionResult> Void(long id, VoidBillRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _billing.VoidAsync(id, request, cancellationToken));
    }
}
