using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonBooking.Application.Features.Scheduling;
using SalonBooking.Application.Interfaces;

namespace SalonBooking.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentWorkflowService _appointments;

    public AppointmentsController(IAppointmentWorkflowService appointments)
    {
        _appointments = appointments;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] AppointmentQueryRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _appointments.GetAppointmentsAsync(request, cancellationToken));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        return Ok(await _appointments.GetAppointmentAsync(id, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _appointments.CreateAppointmentAsync(request, cancellationToken));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateAppointmentRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _appointments.UpdateAppointmentAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:long}/status")]
    public async Task<IActionResult> ChangeStatus(
        long id,
        ChangeAppointmentStatusRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _appointments.ChangeStatusAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(
        long id,
        CancelAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _appointments.CancelAppointmentAsync(id, request, cancellationToken));
    }

    [HttpPost("{appointmentId:long}/services")]
    public async Task<IActionResult> AddService(
        long appointmentId,
        AddAppointmentServiceRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _appointments.AddServiceAsync(appointmentId, request, cancellationToken));
    }

    [HttpPut("{appointmentId:long}/services/{appointmentServiceId:long}")]
    public async Task<IActionResult> UpdateService(
        long appointmentId,
        long appointmentServiceId,
        UpdateAppointmentServiceRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _appointments.UpdateServiceAsync(
            appointmentId, appointmentServiceId, request, cancellationToken));
    }

    [HttpDelete("{appointmentId:long}/services/{appointmentServiceId:long}")]
    public async Task<IActionResult> CancelService(
        long appointmentId,
        long appointmentServiceId,
        [FromQuery] string rowVersion,
        CancellationToken cancellationToken)
    {
        return Ok(await _appointments.CancelServiceAsync(
            appointmentId, appointmentServiceId, rowVersion, cancellationToken));
    }

    [HttpPost("{appointmentId:long}/services/{appointmentServiceId:long}/schedule")]
    public async Task<IActionResult> Schedule(
        long appointmentId,
        long appointmentServiceId,
        ScheduleAppointmentServiceRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _appointments.ScheduleServiceAsync(
            appointmentId, appointmentServiceId, request, cancellationToken));
    }
}
