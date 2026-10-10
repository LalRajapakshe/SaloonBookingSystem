using SalonBooking.Domain.Common;

namespace SalonBooking.Domain.Entities;

public class AppointmentService : TenantEntity
{
    public long AppointmentServiceId { get; set; }

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

    public Appointment Appointment { get; set; } = null!;

    public Service Service { get; set; } = null!;

    public Employee? Employee { get; set; }

    public Branch Branch { get; set; } = null!;

    public ICollection<AppointmentServiceScheduleHistory> ScheduleHistory { get; set; }
        = new List<AppointmentServiceScheduleHistory>();
}
