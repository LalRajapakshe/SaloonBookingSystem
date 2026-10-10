using SalonBooking.Domain.Common;

namespace SalonBooking.Domain.Entities;

public class EmployeeService : TenantEntity
{
    public long EmployeeServiceId { get; set; }

    public long BranchId { get; set; }

    public long EmployeeId { get; set; }

    public long ServiceId { get; set; }

    public int? PreferenceOrder { get; set; }

    public Employee Employee { get; set; } = null!;

    public Service Service { get; set; } = null!;

    public Branch Branch { get; set; } = null!;
}
