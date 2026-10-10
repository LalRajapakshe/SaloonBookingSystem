using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonBooking.Application.Features.Scheduling;
using SalonBooking.Application.Interfaces;

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
    public async Task<IActionResult> Get(
        [FromQuery] StaffLeaveQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _leave.GetAsync(request, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        SaveStaffLeaveRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _leave.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(
        long id,
        SaveStaffLeaveRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _leave.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _leave.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
