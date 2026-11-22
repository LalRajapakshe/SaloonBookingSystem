using SalonBooking.Domain.Enums;

namespace SalonBooking.Application.Features.Scheduling;

public sealed class AppointmentQueryRequest
{
    public long BranchId { get; set; }

    public DateOnly? Date { get; set; }

    public string? Status { get; set; }

    public bool? NeedsScheduling { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

public sealed class CreateOwnAppointmentRequest
{
    public long ServiceId { get; set; }

    public long BranchId { get; set; }

    public long? PreferredEmployeeId { get; set; }

    public DateOnly AppointmentDate { get; set; }

    public DateTime? PreferredStartTime { get; set; }

    public string? Notes { get; set; }
}

public sealed class CustomerServiceChoiceResponse
{
    public long ServiceId { get; set; }

    public long BranchId { get; set; }

    public string ServiceName { get; set; } = string.Empty;

    public int DurationMinutes { get; set; }

    public decimal Price { get; set; }
}

public sealed class CreateAppointmentRequest
{
    public long BranchId { get; set; }

    public long CustomerId { get; set; }

    public DateOnly AppointmentDate { get; set; }

    public string? Status { get; set; }

    public string? Notes { get; set; }
}

public sealed class UpdateAppointmentRequest
{
    public DateOnly AppointmentDate { get; set; }

    public string? Notes { get; set; }
}

public sealed class ChangeAppointmentStatusRequest
{
    public string Status { get; set; } = string.Empty;

    public string? Reason { get; set; }
}

public sealed class CancelAppointmentRequest
{
    public string? Reason { get; set; }
}

public sealed class AddAppointmentServiceRequest
{
    public long ServiceId { get; set; }

    public string? Notes { get; set; }

    public DateTime? PreferredStartTime { get; set; }

    public DateTime? PreferredEndTime { get; set; }
}

public sealed class UpdateAppointmentServiceRequest
{
    public string? Notes { get; set; }

    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ScheduleAppointmentServiceRequest
{
    public long EmployeeId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string? Reason { get; set; }

    public string RowVersion { get; set; } = string.Empty;
}

public readonly record struct CancellationHistoryEntry(
    DateTime ChangedAt,
    long HistoryId,
    AppointmentStatus? OldStatus,
    AppointmentStatus NewStatus,
    string? Reason);

public static class CustomerBookingRequest
{
    public const string NotificationType = "NewBooking";
    public const string RequestedReason = "Requested from the customer app. The time is not reserved.";
    public const string Unallocated = "Unallocated";
    public const string PartiallyScheduled = "PartiallyScheduled";
}

public static class CustomerCancellationRequest
{
    public const string None = "None";
    public const string Requested = "Requested";
    public const string Declined = "Declined";
    public const string Approved = "Approved";
    public const string RequestedReason = "Customer requested cancellation";
    public const string DeclinedReason = "Cancellation request declined";
    public const string ApprovedReason = "Customer cancellation request approved";

    public static string FromHistory(IEnumerable<CancellationHistoryEntry> history)
    {
        var state = None;
        foreach (var item in history.OrderBy(entry => entry.ChangedAt).ThenBy(entry => entry.HistoryId))
        {
            var unchanged = item.OldStatus == item.NewStatus;
            if (unchanged && item.Reason == RequestedReason)
            {
                state = Requested;
                continue;
            }

            if (unchanged && item.Reason == DeclinedReason && state == Requested)
            {
                state = Declined;
                continue;
            }

            if (item.OldStatus != item.NewStatus &&
                item.NewStatus == AppointmentStatus.Cancelled &&
                state == Requested)
            {
                state = item.Reason == ApprovedReason ? Approved : None;
            }
        }

        return state;
    }
}

public sealed class CancellationRequestNotificationResponse
{
    public string NotificationType { get; set; } = "CancellationRequest";

    public long AppointmentId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public List<string> ServiceNames { get; set; } = [];

    public string ServiceName { get; set; } = string.Empty;

    public DateOnly AppointmentDate { get; set; }

    public DateTime? AppointmentTime { get; set; }

    public long BranchId { get; set; }

    public string BranchName { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; }

    public string RequestState { get; set; } = CustomerCancellationRequest.Requested;

    public string? PreferredStylistName { get; set; }

    public string AllocationState { get; set; } = string.Empty;
}

public sealed class AllocationRecommendationResponse
{
    public long AppointmentId { get; set; }

    public DateOnly RequestedDate { get; set; }

    public DateTime? RequestedTime { get; set; }

    public string? PreferredStylistName { get; set; }

    public List<string> ServiceNames { get; set; } = [];

    public List<AllocationOptionResponse> Options { get; set; } = [];
}

public sealed class AllocationOptionResponse
{
    public int Rank { get; set; }

    public bool ExactMatch { get; set; }

    public bool UsesPreferredStylist { get; set; }

    public int MinuteDifference { get; set; }

    public int DayDifference { get; set; }

    public string Reason { get; set; } = string.Empty;

    public List<AllocationLineResponse> Lines { get; set; } = [];
}

public sealed class AllocationLineResponse
{
    public long AppointmentServiceId { get; set; }

    public long ServiceId { get; set; }

    public string ServiceName { get; set; } = string.Empty;

    public int DurationMinutes { get; set; }

    public long EmployeeId { get; set; }

    public string StylistName { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ApplyAllocationRequest
{
    public List<ApplyAllocationLineRequest> Lines { get; set; } = [];
}

public sealed class ApplyAllocationLineRequest
{
    public long AppointmentServiceId { get; set; }

    public long EmployeeId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public string RowVersion { get; set; } = string.Empty;
}

public sealed class AllocationChoice
{
    public DateTime FirstStart { get; init; }

    public int DayDifference { get; init; }

    public int MinuteDifference { get; init; }

    public bool UsesPreferredStylist { get; init; }

    public List<AllocationLineResponse> Lines { get; init; } = [];
}

public static class AllocationRanker
{
    public static IReadOnlyList<AllocationOptionResponse> Rank(IReadOnlyList<AllocationChoice> choices, int take = 5)
    {
        var sameDay = choices.Where(choice => choice.DayDifference == 0).ToList();
        var pool = sameDay.Count > 0 ? sameDay : choices;
        return pool
            .OrderBy(choice => choice.DayDifference)
            .ThenBy(choice => choice.MinuteDifference)
            .ThenByDescending(choice => choice.UsesPreferredStylist)
            .ThenBy(choice => choice.FirstStart)
            .Take(take)
            .Select((choice, index) => new AllocationOptionResponse
            {
                Rank = index + 1,
                ExactMatch = choice.DayDifference == 0 && choice.MinuteDifference == 0,
                UsesPreferredStylist = choice.UsesPreferredStylist,
                MinuteDifference = choice.MinuteDifference,
                DayDifference = choice.DayDifference,
                Reason = ReasonFor(choice),
                Lines = choice.Lines
            })
            .ToList();
    }

    private static string ReasonFor(AllocationChoice choice)
    {
        if (choice.DayDifference == 0 && choice.MinuteDifference == 0)
        {
            return choice.UsesPreferredStylist
                ? "Exact requested time with the preferred stylist."
                : "Exact requested time with an available stylist.";
        }

        if (choice.DayDifference == 0)
        {
            return choice.UsesPreferredStylist
                ? "Closest time on the requested date with the preferred stylist."
                : "Closest available time on the requested date.";
        }

        return choice.UsesPreferredStylist
            ? "No suitable time on the requested date. Nearest day with the preferred stylist."
            : "No suitable time on the requested date. Nearest available day.";
    }
}

public sealed class CustomerOwnAppointmentResponse
{
    public long AppointmentId { get; set; }

    public long BranchId { get; set; }

    public string BranchName { get; set; } = string.Empty;

    public long ServiceId { get; set; }

    public string ServiceName { get; set; } = string.Empty;

    public List<string> ServiceNames { get; set; } = [];

    public DateOnly AppointmentDate { get; set; }

    public DateTime? PreferredStartTime { get; set; }

    public DateTime? ScheduledStartTime { get; set; }

    public string? StylistName { get; set; }

    public string Status { get; set; } = string.Empty;

    public string CancellationRequest { get; set; } = CustomerCancellationRequest.None;

    public DateTime CreatedDate { get; set; }
}

public sealed class AppointmentResponse
{
    public long AppointmentId { get; set; }

    public long TenantId { get; set; }

    public long BranchId { get; set; }

    public long CustomerId { get; set; }

    public DateOnly AppointmentDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public string SchedulingState { get; set; } = string.Empty;

    public bool NeedsScheduling { get; set; }

    public string? Notes { get; set; }

    public DateTime? CancelledAt { get; set; }

    public long? CancelledByUserId { get; set; }

    public string? CancellationReason { get; set; }

    /// None, AwaitingApproval, or Declined. AwaitingApproval does not cancel the visit.
    public string CancellationRequest { get; set; } = "None";

    public List<AppointmentServiceResponse> Services { get; set; } = [];

    public List<AppointmentStatusHistoryResponse> StatusHistory { get; set; } = [];
}

public sealed class AppointmentServiceResponse
{
    public long AppointmentServiceId { get; set; }

    public long AppointmentId { get; set; }

    public long BranchId { get; set; }

    public long ServiceId { get; set; }

    public long? EmployeeId { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public DateTime? PreferredStartTime { get; set; }

    public DateTime? PreferredEndTime { get; set; }

    public int DurationMinutes { get; set; }

    public decimal UnitPrice { get; set; }

    public int Sequence { get; set; }

    public string? Notes { get; set; }

    public bool IsCancelled { get; set; }

    public bool IsFullyScheduled { get; set; }

    public string RowVersion { get; set; } = string.Empty;

    public List<AppointmentServiceScheduleHistoryResponse> ScheduleHistory { get; set; } = [];
}

public sealed class AppointmentStatusHistoryResponse
{
    public long AppointmentStatusHistoryId { get; set; }

    public string? OldStatus { get; set; }

    public string NewStatus { get; set; } = string.Empty;

    public DateTime ChangedAt { get; set; }

    public long? ChangedByUserId { get; set; }

    public string? Reason { get; set; }
}

public sealed class AppointmentServiceScheduleHistoryResponse
{
    public long AppointmentServiceScheduleHistoryId { get; set; }

    public long? OldEmployeeId { get; set; }

    public long? NewEmployeeId { get; set; }

    public DateTime? OldStartTime { get; set; }

    public DateTime? NewStartTime { get; set; }

    public DateTime? OldEndTime { get; set; }

    public DateTime? NewEndTime { get; set; }

    public DateTime ChangedAt { get; set; }

    public long? ChangedByUserId { get; set; }

    public string? Reason { get; set; }
}

public sealed class AssignEmployeeCapabilityRequest
{
    public long BranchId { get; set; }

    public long EmployeeId { get; set; }

    public long ServiceId { get; set; }

    public int? PreferenceOrder { get; set; }
}

public sealed class UpdateEmployeeCapabilityRequest
{
    public bool IsActive { get; set; }

    public int? PreferenceOrder { get; set; }
}

public sealed class EmployeeCapabilityQueryRequest
{
    public long BranchId { get; set; }

    public long? EmployeeId { get; set; }

    public long? ServiceId { get; set; }
}

public sealed class EmployeeCapabilityResponse
{
    public long EmployeeServiceId { get; set; }

    public long TenantId { get; set; }

    public long BranchId { get; set; }

    public long EmployeeId { get; set; }

    public long ServiceId { get; set; }

    public bool IsActive { get; set; }

    public int? PreferenceOrder { get; set; }
}

public sealed class SaveStaffScheduleRequest
{
    public long BranchId { get; set; }

    public long EmployeeId { get; set; }

    public byte DayOfWeek { get; set; }

    public string SegmentType { get; set; } = string.Empty;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class StaffScheduleQueryRequest
{
    public long BranchId { get; set; }

    public long? EmployeeId { get; set; }

    public byte? DayOfWeek { get; set; }
}

public sealed class StaffScheduleResponse
{
    public long StaffScheduleId { get; set; }

    public long TenantId { get; set; }

    public long BranchId { get; set; }

    public long EmployeeId { get; set; }

    public byte DayOfWeek { get; set; }

    public string SegmentType { get; set; } = string.Empty;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public bool IsActive { get; set; }
}

public sealed class SaveStaffLeaveRequest
{
    public long BranchId { get; set; }

    public long EmployeeId { get; set; }

    public DateTime StartDateTime { get; set; }

    public DateTime EndDateTime { get; set; }

    public string LeaveType { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public string Status { get; set; } = string.Empty;
}

public sealed class StaffLeaveQueryRequest
{
    public long BranchId { get; set; }

    public long? EmployeeId { get; set; }

    public string? Status { get; set; }
}

public sealed class StaffLeaveResponse
{
    public long StaffLeaveId { get; set; }

    public long TenantId { get; set; }

    public long BranchId { get; set; }

    public long EmployeeId { get; set; }

    public DateTime StartDateTime { get; set; }

    public DateTime EndDateTime { get; set; }

    public string LeaveType { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public string Status { get; set; } = string.Empty;
}

public sealed class AvailabilityQueryRequest
{
    public long BranchId { get; set; }

    public long ServiceId { get; set; }

    public DateOnly Date { get; set; }

    public long? EmployeeId { get; set; }
}

public sealed class EmployeeAvailabilityResponse
{
    public long EmployeeId { get; set; }

    public string EmployeeName { get; set; } = string.Empty;

    public long ServiceId { get; set; }

    public int DurationMinutes { get; set; }

    public List<AvailabilitySlotResponse> Slots { get; set; } = [];
}

public sealed class AvailabilitySlotResponse
{
    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }
}
