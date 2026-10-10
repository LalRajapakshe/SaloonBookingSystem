using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Scheduling;

namespace SalonBooking.Application.Interfaces;

public interface IAppointmentWorkflowService
{
    Task<PagedResult<AppointmentResponse>> GetAppointmentsAsync(
        AppointmentQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<AppointmentResponse> GetAppointmentAsync(
        long appointmentId,
        CancellationToken cancellationToken = default);

    Task<AppointmentResponse> CreateAppointmentAsync(
        CreateAppointmentRequest request,
        CancellationToken cancellationToken = default);

    Task<AppointmentResponse> UpdateAppointmentAsync(
        long appointmentId,
        UpdateAppointmentRequest request,
        CancellationToken cancellationToken = default);

    Task<AppointmentResponse> ChangeStatusAsync(
        long appointmentId,
        ChangeAppointmentStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<AppointmentResponse> CancelAppointmentAsync(
        long appointmentId,
        CancelAppointmentRequest request,
        CancellationToken cancellationToken = default);

    Task<AppointmentServiceResponse> AddServiceAsync(
        long appointmentId,
        AddAppointmentServiceRequest request,
        CancellationToken cancellationToken = default);

    Task<AppointmentServiceResponse> UpdateServiceAsync(
        long appointmentId,
        long appointmentServiceId,
        UpdateAppointmentServiceRequest request,
        CancellationToken cancellationToken = default);

    Task<AppointmentServiceResponse> CancelServiceAsync(
        long appointmentId,
        long appointmentServiceId,
        string rowVersion,
        CancellationToken cancellationToken = default);

    Task<AppointmentServiceResponse> ScheduleServiceAsync(
        long appointmentId,
        long appointmentServiceId,
        ScheduleAppointmentServiceRequest request,
        CancellationToken cancellationToken = default);
}

public interface IEmployeeCapabilityService
{
    Task<List<EmployeeCapabilityResponse>> GetAsync(
        EmployeeCapabilityQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<EmployeeCapabilityResponse> AssignAsync(
        AssignEmployeeCapabilityRequest request,
        CancellationToken cancellationToken = default);

    Task<EmployeeCapabilityResponse> UpdateAsync(
        long employeeServiceId,
        UpdateEmployeeCapabilityRequest request,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        long employeeServiceId,
        CancellationToken cancellationToken = default);
}

public interface IStaffScheduleService
{
    Task<List<StaffScheduleResponse>> GetAsync(
        StaffScheduleQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<StaffScheduleResponse> CreateAsync(
        SaveStaffScheduleRequest request,
        CancellationToken cancellationToken = default);

    Task<StaffScheduleResponse> UpdateAsync(
        long staffScheduleId,
        SaveStaffScheduleRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        long staffScheduleId,
        CancellationToken cancellationToken = default);
}

public interface IStaffLeaveService
{
    Task<List<StaffLeaveResponse>> GetAsync(
        StaffLeaveQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<StaffLeaveResponse> CreateAsync(
        SaveStaffLeaveRequest request,
        CancellationToken cancellationToken = default);

    Task<StaffLeaveResponse> UpdateAsync(
        long staffLeaveId,
        SaveStaffLeaveRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        long staffLeaveId,
        CancellationToken cancellationToken = default);
}

public interface IAvailabilityQueryService
{
    Task<List<EmployeeAvailabilityResponse>> GetAvailabilityAsync(
        AvailabilityQueryRequest request,
        CancellationToken cancellationToken = default);
}
