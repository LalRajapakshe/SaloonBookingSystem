using SalonBooking.Domain.Common;
using SalonBooking.Domain.Enums;

namespace SalonBooking.Domain.Entities;

public class StaffSchedule : TenantEntity
{
    public long StaffScheduleId { get; set; }

    public long BranchId { get; set; }

    public long EmployeeId { get; set; }

    public byte DayOfWeek { get; set; }

    public ScheduleSegmentType SegmentType { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public Employee Employee { get; set; } = null!;

    public Branch Branch { get; set; } = null!;
}
