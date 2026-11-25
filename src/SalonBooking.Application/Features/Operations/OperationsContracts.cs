namespace SalonBooking.Application.Features.Operations;

public sealed class OperationsDashboardResponse
{
    public DateOnly Date { get; set; }
    public string Scope { get; set; } = "Branch";
    public int TodayAppointments { get; set; }
    public int Pending { get; set; }
    public int AwaitingConfirmation { get; set; }
    public int CheckedIn { get; set; }
    public int InProgress { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
    public int NoShow { get; set; }
    public int OpenServiceLines { get; set; }
    public int MyAssignedLines { get; set; }
    public int? ActiveRentals { get; set; }
    public int? OverdueRentals { get; set; }
    public int? PendingFeedback { get; set; }
    public int? UnpaidBills { get; set; }
    public decimal? OutstandingAmount { get; set; }
}
