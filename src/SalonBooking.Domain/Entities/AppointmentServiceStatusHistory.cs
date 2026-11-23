using SalonBooking.Domain.Common;
using SalonBooking.Domain.Enums;

namespace SalonBooking.Domain.Entities;

public class AppointmentServiceStatusHistory : TenantEntity
{
    public long AppointmentServiceStatusHistoryId { get; set; }

    public long BranchId { get; set; }

    public long AppointmentId { get; set; }

    public long AppointmentServiceId { get; set; }

    public ServiceLineStatus? OldStatus { get; set; }

    public ServiceLineStatus NewStatus { get; set; }

    public long? EmployeeId { get; set; }

    public DateTime ChangedAt { get; set; }

    public long? ChangedByUserId { get; set; }

    public string? Reason { get; set; }

    public string Action { get; set; } = string.Empty;

    public Appointment? Appointment { get; set; }

    public AppointmentService AppointmentService { get; set; } = null!;

    public Branch Branch { get; set; } = null!;

    public User? ChangedByUser { get; set; }
}
