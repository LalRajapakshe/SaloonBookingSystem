using SalonBooking.Domain.Common;
using SalonBooking.Domain.Enums;

namespace SalonBooking.Domain.Entities;

public class StaffLeave : TenantEntity
{
    public long StaffLeaveId { get; set; }

    public long BranchId { get; set; }

    public long EmployeeId { get; set; }

    public DateTime StartDateTime { get; set; }

    public DateTime EndDateTime { get; set; }

    public LeaveType LeaveType { get; set; }

    public string? Reason { get; set; }

    public LeaveStatus Status { get; set; }

    public Employee Employee { get; set; } = null!;

    public Branch Branch { get; set; } = null!;
}
