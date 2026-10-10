using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonBooking.Application.Features.Scheduling;
using SalonBooking.Application.Interfaces;

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
    public async Task<IActionResult> Get(
        [FromQuery] StaffScheduleQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _schedules.GetAsync(request, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        SaveStaffScheduleRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _schedules.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(
        long id,
        SaveStaffScheduleRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _schedules.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await _schedules.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
