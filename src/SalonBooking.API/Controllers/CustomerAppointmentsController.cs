using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonBooking.API.Authorization;
using SalonBooking.Application.Features.Scheduling;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Security;

namespace SalonBooking.API.Controllers;

[Authorize]
[ApiController]
[Route("api/CustomerAppointments")]
public class CustomerAppointmentsController : ControllerBase
{
    private readonly IAppointmentWorkflowService _appointments;

    public CustomerAppointmentsController(IAppointmentWorkflowService appointments)
    {
        _appointments = appointments;
    }

    [HttpGet("services")]
    [RequiresPermission(PermissionCatalog.CustomerAppointmentsCreate)]
    public async Task<IActionResult> Services(CancellationToken cancellationToken)
    {
        return Ok(await _appointments.ListOwnServicesAsync(cancellationToken));
    }

    [HttpPost]
    [RequiresPermission(PermissionCatalog.CustomerAppointmentsCreate)]
    public async Task<IActionResult> Create(
        CreateOwnAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _appointments.CreateOwnAppointmentAsync(request, cancellationToken));
    }
}
