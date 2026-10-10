using SalonBooking.Domain.Common;
using SalonBooking.Domain.Enums;

namespace SalonBooking.Domain.Entities;

public class AppointmentStatusHistory : TenantEntity
{
    public long AppointmentStatusHistoryId { get; set; }

    public long BranchId { get; set; }

    public long AppointmentId { get; set; }

    public AppointmentStatus? OldStatus { get; set; }

    public AppointmentStatus NewStatus { get; set; }

    public DateTime ChangedAt { get; set; }

    public long? ChangedByUserId { get; set; }

    public string? Reason { get; set; }

    public Appointment Appointment { get; set; } = null!;

    public Branch Branch { get; set; } = null!;

    public User? ChangedByUser { get; set; }
}
