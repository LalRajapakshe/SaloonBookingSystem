namespace SalonBooking.Application.Features.Feedback;

public sealed class PendingFeedbackResponse
{
    public long AppointmentServiceId { get; set; }
    public long AppointmentId { get; set; }
    public long BranchId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string StylistName { get; set; } = string.Empty;
    public DateOnly AppointmentDate { get; set; }
}

public sealed class SubmitFeedbackRequest
{
    public long AppointmentServiceId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

public sealed class ServiceFeedbackResponse
{
    public long ServiceFeedbackId { get; set; }
    public long AppointmentServiceId { get; set; }
    public long EmployeeId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime SubmittedAt { get; set; }
}
