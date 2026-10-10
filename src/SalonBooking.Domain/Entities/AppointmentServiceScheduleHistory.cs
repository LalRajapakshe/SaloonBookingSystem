using SalonBooking.Domain.Common;

namespace SalonBooking.Domain.Entities;

public class AppointmentServiceScheduleHistory : TenantEntity
{
    public long AppointmentServiceScheduleHistoryId { get; set; }

    public long BranchId { get; set; }

    public long AppointmentServiceId { get; set; }

    public long? OldEmployeeId { get; set; }

    public long? NewEmployeeId { get; set; }

    public DateTime? OldStartTime { get; set; }

    public DateTime? NewStartTime { get; set; }

    public DateTime? OldEndTime { get; set; }

    public DateTime? NewEndTime { get; set; }

    public DateTime ChangedAt { get; set; }

    public long? ChangedByUserId { get; set; }

    public string? Reason { get; set; }

    public AppointmentService AppointmentService { get; set; } = null!;

    public Branch Branch { get; set; } = null!;

    public Employee? OldEmployee { get; set; }

    public Employee? NewEmployee { get; set; }

    public User? ChangedByUser { get; set; }
}
