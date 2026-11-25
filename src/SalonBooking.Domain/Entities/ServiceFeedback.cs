using SalonBooking.Domain.Common;

namespace SalonBooking.Domain.Entities;

/// <summary>
/// One customer rating for one completed service line and its actual stylist.
/// This is not a stylist grade.
/// </summary>
public class ServiceFeedback : TenantEntity
{
    public long ServiceFeedbackId { get; set; }

    public long BranchId { get; set; }

    public long CustomerId { get; set; }

    public long AppointmentId { get; set; }

    public long AppointmentServiceId { get; set; }

    public long EmployeeId { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime SubmittedAt { get; set; }

    public AppointmentService AppointmentService { get; set; } = null!;

    public Employee Employee { get; set; } = null!;
}
