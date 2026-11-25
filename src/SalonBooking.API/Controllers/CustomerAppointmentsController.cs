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
    private readonly IFeedbackService _feedback;

    public CustomerAppointmentsController(IAppointmentWorkflowService appointments, IFeedbackService feedback)
    {
        _appointments = appointments;
        _feedback = feedback;
    }

    [HttpGet("services")]
    [RequiresPermission(PermissionCatalog.CustomerAppointmentsCreate)]
    public async Task<IActionResult> Services(CancellationToken cancellationToken)
    {
        await _feedback.EnsureCustomerMayContinueAsync(cancellationToken);
        return Ok(await _appointments.ListOwnServicesAsync(cancellationToken));
    }

    [HttpGet]
    [RequiresPermission(PermissionCatalog.CustomerAppointmentsRead)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        return Ok(await _appointments.ListOwnAppointmentsAsync(cancellationToken));
    }

    [HttpPost]
    [RequiresPermission(PermissionCatalog.CustomerAppointmentsCreate)]
    public async Task<IActionResult> Create(
        CreateOwnAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        await _feedback.EnsureCustomerMayContinueAsync(cancellationToken);
        return Ok(await _appointments.CreateOwnAppointmentAsync(request, cancellationToken));
    }

    [HttpPost("{id:long}/cancellation-request")]
    [RequiresPermission(PermissionCatalog.CustomerAppointmentsCreate)]
    public async Task<IActionResult> RequestCancellation(long id, CancellationToken cancellationToken)
    {
        await _feedback.EnsureCustomerMayContinueAsync(cancellationToken);
        return Ok(await _appointments.RequestOwnCancellationAsync(id, cancellationToken));
    }
}
