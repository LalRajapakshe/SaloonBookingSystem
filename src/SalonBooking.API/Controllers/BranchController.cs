using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Features.Branch.DTOs;

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
    public async Task<IActionResult> Create(CreateBranchRequest request)
    {
        var result = await _branchService.CreateAsync(request);

        return Ok(result);
    }
    [HttpGet]
    public async Task<IActionResult> GetBranch(
        [FromQuery] BranchQueryRequest request)
    {
        var result = await _branchService.GetBranchAsync(request);

        return Ok(result);
    }

        [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var tenant = await _branchService.GetByIdAsync(id);

        if (tenant == null)
            return NotFound();

        return Ok(tenant);
    }

     [HttpPut("{id:long}")]
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
    public async Task<IActionResult> Delete(long id)
    {
        await _branchService.DeleteAsync(id);

        return NoContent();
    }
}