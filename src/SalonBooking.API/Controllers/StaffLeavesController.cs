using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonBooking.API.Authorization;
using SalonBooking.Application.Features.Scheduling;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Security;

namespace SalonBooking.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class StaffLeavesController : ControllerBase
{
    private readonly IStaffLeaveService _leave;

    public StaffLeavesController(IStaffLeaveService leave)
    {
        _leave = leave;
    }

    [HttpGet]
    [RequiresPermission(PermissionCatalog.LeavesRead)]
    public async Task<IActionResult> Get(
        [FromQuery] StaffLeaveQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _leave.GetAsync(request, cancellationToken));
    }

    [HttpPost]
    [RequiresPermission(PermissionCatalog.LeavesManage)]
    public async Task<IActionResult> Create(
        SaveStaffLeaveRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _leave.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:long}")]
    [RequiresPermission(PermissionCatalog.LeavesManage)]
    public async Task<IActionResult> Update(
        long id,
        SaveStaffLeaveRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _leave.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:long}")]
    [RequiresPermission(PermissionCatalog.LeavesManage)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _leave.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
