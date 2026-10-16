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
public class StaffSchedulesController : ControllerBase
{
    private readonly IStaffScheduleService _schedules;

    public StaffSchedulesController(IStaffScheduleService schedules)
    {
        _schedules = schedules;
    }

    [HttpGet]
    [RequiresPermission(PermissionCatalog.SchedulesRead)]
    public async Task<IActionResult> Get(
        [FromQuery] StaffScheduleQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _schedules.GetAsync(request, cancellationToken));
    }

    [HttpPost]
    [RequiresPermission(PermissionCatalog.SchedulesManage)]
    public async Task<IActionResult> Create(
        SaveStaffScheduleRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _schedules.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:long}")]
    [RequiresPermission(PermissionCatalog.SchedulesManage)]
    public async Task<IActionResult> Update(
        long id,
        SaveStaffScheduleRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _schedules.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:long}")]
    [RequiresPermission(PermissionCatalog.SchedulesManage)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _schedules.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
