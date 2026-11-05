using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonBooking.API.Authorization;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Security;

namespace SalonBooking.API.Controllers;

[Authorize]
[ApiController]
[Route("api/CustomerCatalogue")]
public class CustomerCatalogueController : ControllerBase
{
    private readonly ICustomerCatalogueService _catalogue;

    public CustomerCatalogueController(ICustomerCatalogueService catalogue)
    {
        _catalogue = catalogue;
    }

    [HttpGet("branches")]
    [RequiresPermission(PermissionCatalog.CustomerCatalogueRead)]
    public async Task<IActionResult> Branches(CancellationToken cancellationToken)
    {
        return Ok(await _catalogue.ListBranchesAsync(cancellationToken));
    }

    [HttpGet("branches/{branchId:long}")]
    [RequiresPermission(PermissionCatalog.CustomerCatalogueRead)]
    public async Task<IActionResult> Branch(long branchId, CancellationToken cancellationToken)
    {
        return Ok(await _catalogue.GetBranchAsync(branchId, cancellationToken));
    }

    [HttpGet("branches/{branchId:long}/services")]
    [RequiresPermission(PermissionCatalog.CustomerCatalogueRead)]
    public async Task<IActionResult> Services(long branchId, CancellationToken cancellationToken)
    {
        return Ok(await _catalogue.ListServicesAsync(branchId, cancellationToken));
    }

    [HttpGet("branches/{branchId:long}/services/{serviceId:long}")]
    [RequiresPermission(PermissionCatalog.CustomerCatalogueRead)]
    public async Task<IActionResult> Service(long branchId, long serviceId, CancellationToken cancellationToken)
    {
        return Ok(await _catalogue.GetServiceAsync(branchId, serviceId, cancellationToken));
    }

    [HttpGet("branches/{branchId:long}/services/{serviceId:long}/stylists")]
    [RequiresPermission(PermissionCatalog.CustomerCatalogueRead)]
    public async Task<IActionResult> Stylists(long branchId, long serviceId, CancellationToken cancellationToken)
    {
        return Ok(await _catalogue.ListStylistsAsync(branchId, serviceId, cancellationToken));
    }

    [HttpGet("stylists/{employeeId:long}")]
    [RequiresPermission(PermissionCatalog.CustomerCatalogueRead)]
    public async Task<IActionResult> Stylist(long employeeId, CancellationToken cancellationToken)
    {
        return Ok(await _catalogue.GetStylistAsync(employeeId, cancellationToken));
    }

    [HttpGet("availability")]
    [RequiresPermission(PermissionCatalog.CustomerAvailabilityRead)]
    public async Task<IActionResult> Availability(
        [FromQuery] long branchId,
        [FromQuery] long serviceId,
        [FromQuery] DateOnly date,
        [FromQuery] long? employeeId,
        CancellationToken cancellationToken)
    {
        return Ok(await _catalogue.GetAvailabilityAsync(branchId, serviceId, date, employeeId, cancellationToken));
    }

    [HttpGet("branches/{branchId:long}/photo")]
    [RequiresPermission(PermissionCatalog.CustomerCatalogueRead)]
    public async Task<IActionResult> BranchPhoto(long branchId, CancellationToken cancellationToken)
    {
        return Photo(await _catalogue.OpenBranchPhotoAsync(branchId, cancellationToken));
    }

    [HttpGet("services/{serviceId:long}/photo")]
    [RequiresPermission(PermissionCatalog.CustomerCatalogueRead)]
    public async Task<IActionResult> ServicePhoto(long serviceId, CancellationToken cancellationToken)
    {
        return Photo(await _catalogue.OpenServicePhotoAsync(serviceId, cancellationToken));
    }

    [HttpGet("stylists/{employeeId:long}/photo")]
    [RequiresPermission(PermissionCatalog.CustomerCatalogueRead)]
    public async Task<IActionResult> StylistPhoto(long employeeId, CancellationToken cancellationToken)
    {
        return Photo(await _catalogue.OpenStylistPhotoAsync(employeeId, cancellationToken));
    }

    private IActionResult Photo(Application.Common.StoredFileContent? file)
    {
        if (file == null)
        {
            return NotFound();
        }

        return File(file.Content, file.ContentType);
    }
}
