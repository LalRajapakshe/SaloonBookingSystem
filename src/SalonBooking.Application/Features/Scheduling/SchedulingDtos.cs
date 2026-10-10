namespace SalonBooking.Application.Features.Scheduling;

public sealed class AppointmentQueryRequest
{
    public long BranchId { get; set; }

    public DateOnly? Date { get; set; }

    public string? Status { get; set; }

    public bool? NeedsScheduling { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

public sealed class CreateAppointmentRequest
{
    public long BranchId { get; set; }

    public long CustomerId { get; set; }

    public DateOnly AppointmentDate { get; set; }

    public string? Status { get; set; }

    public string? Notes { get; set; }
}

public sealed class UpdateAppointmentRequest
{
    public DateOnly AppointmentDate { get; set; }

    public string? Notes { get; set; }
}

public sealed class ChangeAppointmentStatusRequest
{
    public string Status { get; set; } = string.Empty;

    public string? Reason { get; set; }
}

public sealed class CancelAppointmentRequest
{
    public string? Reason { get; set; }
}

public sealed class AddAppointmentServiceRequest
{
    public long ServiceId { get; set; }

    public string? Notes { get; set; }
}

public sealed class UpdateAppointmentServiceRequest
{
    public string? Notes { get; set; }

    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ScheduleAppointmentServiceRequest
{
    public long EmployeeId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string? Reason { get; set; }

    public string RowVersion { get; set; } = string.Empty;
}

public sealed class AppointmentResponse
{
    public long AppointmentId { get; set; }

    public long TenantId { get; set; }

    public long BranchId { get; set; }

    public long CustomerId { get; set; }

    public DateOnly AppointmentDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public string SchedulingState { get; set; } = string.Empty;

    public bool NeedsScheduling { get; set; }

    public string? Notes { get; set; }

    public DateTime? CancelledAt { get; set; }

    public long? CancelledByUserId { get; set; }

    public string? CancellationReason { get; set; }

    public List<AppointmentServiceResponse> Services { get; set; } = [];

    public List<AppointmentStatusHistoryResponse> StatusHistory { get; set; } = [];
}

public sealed class AppointmentServiceResponse
{
    public long AppointmentServiceId { get; set; }

    public long AppointmentId { get; set; }

    public long BranchId { get; set; }

    public long ServiceId { get; set; }

    public long? EmployeeId { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public int DurationMinutes { get; set; }

    public decimal UnitPrice { get; set; }

    public int Sequence { get; set; }

    public string? Notes { get; set; }

    public bool IsCancelled { get; set; }

    public bool IsFullyScheduled { get; set; }

    public string RowVersion { get; set; } = string.Empty;

    public List<AppointmentServiceScheduleHistoryResponse> ScheduleHistory { get; set; } = [];
}

public sealed class AppointmentStatusHistoryResponse
{
    public long AppointmentStatusHistoryId { get; set; }

    public string? OldStatus { get; set; }

    public string NewStatus { get; set; } = string.Empty;

    public DateTime ChangedAt { get; set; }

    public long? ChangedByUserId { get; set; }

    public string? Reason { get; set; }
}

public sealed class AppointmentServiceScheduleHistoryResponse
{
    public long AppointmentServiceScheduleHistoryId { get; set; }

    public long? OldEmployeeId { get; set; }

    public long? NewEmployeeId { get; set; }

    public DateTime? OldStartTime { get; set; }

    public DateTime? NewStartTime { get; set; }

    public DateTime? OldEndTime { get; set; }

    public DateTime? NewEndTime { get; set; }

    public DateTime ChangedAt { get; set; }

    public long? ChangedByUserId { get; set; }

    public string? Reason { get; set; }
}

public sealed class AssignEmployeeCapabilityRequest
{
    public long BranchId { get; set; }

    public long EmployeeId { get; set; }

    public long ServiceId { get; set; }

    public int? PreferenceOrder { get; set; }
}

public sealed class UpdateEmployeeCapabilityRequest
{
    public bool IsActive { get; set; }

    public int? PreferenceOrder { get; set; }
}

public sealed class EmployeeCapabilityQueryRequest
{
    public long BranchId { get; set; }

    public long? EmployeeId { get; set; }

    public long? ServiceId { get; set; }
}

public sealed class EmployeeCapabilityResponse
{
    public long EmployeeServiceId { get; set; }

    public long TenantId { get; set; }

    public long BranchId { get; set; }

    public long EmployeeId { get; set; }

    public long ServiceId { get; set; }

    public bool IsActive { get; set; }

    public int? PreferenceOrder { get; set; }
}

public sealed class SaveStaffScheduleRequest
{
    public long BranchId { get; set; }

    public long EmployeeId { get; set; }

    public byte DayOfWeek { get; set; }

    public string SegmentType { get; set; } = string.Empty;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class StaffScheduleQueryRequest
{
    public long BranchId { get; set; }

    public long? EmployeeId { get; set; }

    public byte? DayOfWeek { get; set; }
}

public sealed class StaffScheduleResponse
{
    public long StaffScheduleId { get; set; }

    public long TenantId { get; set; }

    public long BranchId { get; set; }

    public long EmployeeId { get; set; }

    public byte DayOfWeek { get; set; }

    public string SegmentType { get; set; } = string.Empty;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public bool IsActive { get; set; }
}

public sealed class SaveStaffLeaveRequest
{
    public long BranchId { get; set; }

    public long EmployeeId { get; set; }

    public DateTime StartDateTime { get; set; }

    public DateTime EndDateTime { get; set; }

    public string LeaveType { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public string Status { get; set; } = string.Empty;
}

public sealed class StaffLeaveQueryRequest
{
    public long BranchId { get; set; }

    public long? EmployeeId { get; set; }

    public string? Status { get; set; }
}

public sealed class StaffLeaveResponse
{
    public long StaffLeaveId { get; set; }

    public long TenantId { get; set; }

    public long BranchId { get; set; }

    public long EmployeeId { get; set; }

    public DateTime StartDateTime { get; set; }

    public DateTime EndDateTime { get; set; }

    public string LeaveType { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public string Status { get; set; } = string.Empty;
}

public sealed class AvailabilityQueryRequest
{
    public long BranchId { get; set; }

    public long ServiceId { get; set; }

    public DateOnly Date { get; set; }

    public long? EmployeeId { get; set; }
}

public sealed class EmployeeAvailabilityResponse
{
    public long EmployeeId { get; set; }

    public string EmployeeName { get; set; } = string.Empty;

    public long ServiceId { get; set; }

    public int DurationMinutes { get; set; }

    public List<AvailabilitySlotResponse> Slots { get; set; } = [];
}

public sealed class AvailabilitySlotResponse
{
    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }
}
