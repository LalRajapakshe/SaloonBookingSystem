using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SalonBooking.API.Authorization;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Features.Branch.DTOs;
using SalonBooking.Application.Security;
using SalonBooking.Infrastructure.Services;

namespace SalonBooking.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class BranchController : ControllerBase
{
    private readonly IBranchService _branchService;
    private readonly CatalogueImageAdminService _photos;

    public BranchController(IBranchService branchService, CatalogueImageAdminService photos)
    {
        _branchService = branchService;
        _photos = photos;
    }

    [HttpPost]
    [RequiresPermission(PermissionCatalog.BranchesManage)]
    public async Task<IActionResult> Create(CreateBranchRequest request)
    {
        var result = await _branchService.CreateAsync(request);

        return Ok(result);
    }
    [HttpGet]
    [RequiresPermission(PermissionCatalog.BranchesRead)]
    public async Task<IActionResult> GetBranch(
        [FromQuery] BranchQueryRequest request)
    {
        var result = await _branchService.GetBranchAsync(request);

        return Ok(result);
    }

        [HttpGet("{id:long}")]
    [RequiresPermission(PermissionCatalog.BranchesRead)]
    public async Task<IActionResult> GetById(long id)
    {
        var tenant = await _branchService.GetByIdAsync(id);

        if (tenant == null)
            return NotFound();

        return Ok(tenant);
    }

     [HttpPut("{id:long}")]
    [RequiresPermission(PermissionCatalog.BranchesManage)]
    public async Task<IActionResult> Update(
        long id,
        UpdateBranchRequest request)
    {
        var tenant =
            await _branchService.UpdateAsync(id, request);

        if (tenant == null)
            return NotFound();    

        return Ok(tenant);
    }

    [HttpDelete("{id:long}")]
    [RequiresPermission(PermissionCatalog.BranchesManage)]
    public async Task<IActionResult> Delete(long id)
    {
        await _branchService.DeleteAsync(id);

        return NoContent();
    }

    [HttpPost("{id:long}/photo")]
    [RequiresPermission(PermissionCatalog.BranchesManage)]
    [RequestSizeLimit(5_242_880)]
    public async Task<IActionResult> SavePhoto(long id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { title = "A photo file is required." });
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return Ok(await _photos.SaveBranchPhotoAsync(id, buffer.ToArray(), cancellationToken));
    }

    [HttpGet("{id:long}/photo")]
    [RequiresPermission(PermissionCatalog.BranchesRead)]
    public async Task<IActionResult> Photo(long id, CancellationToken cancellationToken)
    {
        var photo = await _photos.OpenBranchPhotoAsync(id, cancellationToken);
        return photo == null ? NotFound() : File(photo.Content, photo.ContentType);
    }

    [HttpDelete("{id:long}/photo")]
    [RequiresPermission(PermissionCatalog.BranchesManage)]
    public async Task<IActionResult> RemovePhoto(long id, CancellationToken cancellationToken)
    {
        return Ok(await _photos.RemoveBranchPhotoAsync(id, cancellationToken));
    }
}