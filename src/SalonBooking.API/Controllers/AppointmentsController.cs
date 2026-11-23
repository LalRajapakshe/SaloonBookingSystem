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
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentWorkflowService _appointments;

    public AppointmentsController(IAppointmentWorkflowService appointments)
    {
        _appointments = appointments;
    }

    [HttpGet]
    [RequiresPermission(PermissionCatalog.AppointmentsRead)]
    public async Task<IActionResult> Get([FromQuery] AppointmentQueryRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _appointments.GetAppointmentsAsync(request, cancellationToken));
    }

    [HttpGet("cancellation-requests")]
    [HttpGet("notifications")]
    [RequiresAnyPermission]
    public async Task<IActionResult> Notifications([FromQuery] long? branchId, CancellationToken cancellationToken)
    {
        return Ok(await _appointments.ListStaffNotificationsAsync(cancellationToken, branchId));
    }

    [HttpGet("mine")]
    [RequiresPermission(PermissionCatalog.AppointmentsOwnRead)]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken)
    {
        return Ok(await _appointments.ListMyAssignedAppointmentsAsync(cancellationToken));
    }

    [HttpGet("{id:long}/allocation-recommendations")]
    [RequiresPermission(PermissionCatalog.AppointmentsRead)]
    public async Task<IActionResult> RecommendAllocation(long id, CancellationToken cancellationToken)
    {
        return Ok(await _appointments.RecommendAllocationAsync(id, cancellationToken));
    }

    [HttpPost("{id:long}/allocate")]
    [RequiresPermission(PermissionCatalog.AppointmentsManage)]
    public async Task<IActionResult> Allocate(long id, ApplyAllocationRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _appointments.ApplyAllocationAsync(id, request, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [RequiresPermission(PermissionCatalog.AppointmentsRead)]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        return Ok(await _appointments.GetAppointmentAsync(id, cancellationToken));
    }

    [HttpPost]
    [RequiresPermission(PermissionCatalog.AppointmentsManage)]
    public async Task<IActionResult> Create(CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _appointments.CreateAppointmentAsync(request, cancellationToken));
    }

    [HttpPut("{id:long}")]
    [RequiresPermission(PermissionCatalog.AppointmentsManage)]
    public async Task<IActionResult> Update(long id, UpdateAppointmentRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _appointments.UpdateAppointmentAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:long}/status")]
    [RequiresPermission(PermissionCatalog.AppointmentsManage)]
    public async Task<IActionResult> ChangeStatus(
        long id,
        ChangeAppointmentStatusRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _appointments.ChangeStatusAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:long}/cancel")]
    [RequiresPermission(PermissionCatalog.AppointmentsManage)]
    public async Task<IActionResult> Cancel(
        long id,
        CancelAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _appointments.CancelAppointmentAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:long}/decline-cancellation")]
    [RequiresPermission(PermissionCatalog.AppointmentsManage)]
    public async Task<IActionResult> DeclineCancellation(long id, CancellationToken cancellationToken)
    {
        return Ok(await _appointments.DeclineCancellationRequestAsync(id, cancellationToken));
    }

    [HttpPost("{id:long}/approve-cancellation")]
    [RequiresPermission(PermissionCatalog.AppointmentsManage)]
    public async Task<IActionResult> ApproveCancellation(long id, CancellationToken cancellationToken)
    {
        return Ok(await _appointments.ApproveCancellationRequestAsync(id, cancellationToken));
    }

    [HttpPost("{appointmentId:long}/services")]
    [RequiresPermission(PermissionCatalog.AppointmentsManage)]
    public async Task<IActionResult> AddService(
        long appointmentId,
        AddAppointmentServiceRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _appointments.AddServiceAsync(appointmentId, request, cancellationToken));
    }

    [HttpPut("{appointmentId:long}/services/{appointmentServiceId:long}")]
    [RequiresPermission(PermissionCatalog.AppointmentsManage)]
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
    [RequiresPermission(PermissionCatalog.AppointmentsManage)]
    public async Task<IActionResult> CancelService(
        long appointmentId,
        long appointmentServiceId,
        [FromQuery] string rowVersion,
        CancellationToken cancellationToken)
    {
        return Ok(await _appointments.CancelServiceAsync(
            appointmentId, appointmentServiceId, rowVersion, cancellationToken));
    }

    [HttpGet("{appointmentId:long}/services/{appointmentServiceId:long}/progress")]
    [RequiresAnyPermission(RequiresAnyPermissionAttribute.LineHistoryPolicy)]
    public async Task<IActionResult> LineProgressHistory(
        long appointmentId,
        long appointmentServiceId,
        CancellationToken cancellationToken)
    {
        return Ok(await _appointments.ListLineProgressHistoryAsync(
            appointmentId,
            appointmentServiceId,
            cancellationToken));
    }

    [HttpPost("{appointmentId:long}/services/{appointmentServiceId:long}/progress")]
    [RequiresAnyPermission(RequiresAnyPermissionAttribute.LineProgressPolicy)]
    public async Task<IActionResult> ProgressOwn(
        long appointmentId,
        long appointmentServiceId,
        ProgressOwnServiceRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _appointments.ProgressOwnServiceAsync(
            appointmentId,
            appointmentServiceId,
            request,
            cancellationToken));
    }

    [HttpPost("{appointmentId:long}/services/{appointmentServiceId:long}/schedule")]
    [RequiresPermission(PermissionCatalog.AppointmentsManage)]
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
