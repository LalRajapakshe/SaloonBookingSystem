using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SalonBooking.API.Authorization;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Features.Branch.DTOs;
using SalonBooking.Application.Security;

namespace SalonBooking.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class BranchController : ControllerBase
{
    private readonly IBranchService _branchService;

    public BranchController(IBranchService branchService)
    {
        _branchService = branchService;
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
}