using SalonBooking.Domain.Common;
using SalonBooking.Domain.Enums;

namespace SalonBooking.Domain.Entities;

public class Appointment : TenantEntity
{
    public long AppointmentId { get; set; }

    public long BranchId { get; set; }

    public long CustomerId { get; set; }

    public DateOnly AppointmentDate { get; set; }

    public AppointmentStatus Status { get; set; }

    public string? Notes { get; set; }

    public DateTime? CancelledAt { get; set; }

    public long? CancelledByUserId { get; set; }

    public string? CancellationReason { get; set; }

    public Customer Customer { get; set; } = null!;

    public Branch Branch { get; set; } = null!;

    public User? CancelledByUser { get; set; }

    public ICollection<AppointmentService> AppointmentServices { get; set; }
        = new List<AppointmentService>();

    public ICollection<AppointmentStatusHistory> StatusHistory { get; set; }
        = new List<AppointmentStatusHistory>();
}
