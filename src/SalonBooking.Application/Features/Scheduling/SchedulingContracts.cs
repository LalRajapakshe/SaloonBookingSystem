namespace SalonBooking.Application.Features.Scheduling;

public sealed class AppointmentContract
{
    public long AppointmentId { get; set; }

    public long TenantId { get; set; }

    public long BranchId { get; set; }

    public long CustomerId { get; set; }

    public DateOnly AppointmentDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTime? CancelledAt { get; set; }

    public long? CancelledByUserId { get; set; }

    public string? CancellationReason { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? ModifiedDate { get; set; }

    public bool IsDeleted { get; set; }
}

public sealed class AppointmentServiceContract
{
    public long AppointmentServiceId { get; set; }

    public long TenantId { get; set; }

    public long BranchId { get; set; }

    public long AppointmentId { get; set; }

    public long ServiceId { get; set; }

    public long? EmployeeId { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public int DurationMinutes { get; set; }

    public decimal UnitPrice { get; set; }

    public int Sequence { get; set; }

    public string? Notes { get; set; }

    public bool IsCancelled { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public DateTime CreatedDate { get; set; }

    public DateTime? ModifiedDate { get; set; }

    public bool IsDeleted { get; set; }
}

public sealed class EmployeeServiceContract
{
    public long EmployeeServiceId { get; set; }

    public long TenantId { get; set; }

    public long BranchId { get; set; }

    public long EmployeeId { get; set; }

    public long ServiceId { get; set; }

    public bool IsActive { get; set; }

    public int? PreferenceOrder { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? ModifiedDate { get; set; }

    public bool IsDeleted { get; set; }
}

public sealed class StaffScheduleContract
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

    public DateTime CreatedDate { get; set; }

    public DateTime? ModifiedDate { get; set; }

    public bool IsDeleted { get; set; }
}

public sealed class StaffLeaveContract
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

    public DateTime CreatedDate { get; set; }

    public DateTime? ModifiedDate { get; set; }

    public bool IsDeleted { get; set; }
}
