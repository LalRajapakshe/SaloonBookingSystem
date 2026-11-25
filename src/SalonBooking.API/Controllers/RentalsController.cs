using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonBooking.API.Authorization;
using SalonBooking.Application.Features.Rentals;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Security;

namespace SalonBooking.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RentalsController : ControllerBase
{
    private readonly IRentalService _rentals;

    public RentalsController(IRentalService rentals)
    {
        _rentals = rentals;
    }

    [HttpGet("assets")]
    [RequiresPermission(PermissionCatalog.RentalsRead)]
    public async Task<IActionResult> Assets([FromQuery] bool includeInactive, CancellationToken cancellationToken)
    {
        return Ok(await _rentals.ListAssetsAsync(includeInactive, cancellationToken));
    }

    [HttpPost("assets")]
    [RequiresPermission(PermissionCatalog.RentalsManage)]
    public async Task<IActionResult> SaveAsset(SaveRentalAssetRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _rentals.SaveAssetAsync(request, cancellationToken));
    }

    [HttpGet("reservations")]
    [RequiresPermission(PermissionCatalog.RentalsRead)]
    public async Task<IActionResult> Reservations(CancellationToken cancellationToken)
    {
        return Ok(await _rentals.ListReservationsAsync(cancellationToken));
    }

    [HttpPost("reservations")]
    [RequiresPermission(PermissionCatalog.RentalsManage)]
    public async Task<IActionResult> Reserve(CreateRentalReservationRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _rentals.CreateReservationAsync(request, cancellationToken));
    }

    [HttpPost("reservations/{id:long}/cancel")]
    [RequiresPermission(PermissionCatalog.RentalsManage)]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        return Ok(await _rentals.CancelReservationAsync(id, cancellationToken));
    }

    [HttpPost("reservations/{id:long}/issue")]
    [RequiresPermission(PermissionCatalog.RentalsIssue)]
    public async Task<IActionResult> Issue(long id, RentalMovementRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _rentals.IssueAsync(id, request, cancellationToken));
    }

    [HttpPost("reservations/{id:long}/return")]
    [RequiresPermission(PermissionCatalog.RentalsIssue)]
    public async Task<IActionResult> Return(long id, RentalMovementRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _rentals.ReturnAsync(id, request, cancellationToken));
    }
}
