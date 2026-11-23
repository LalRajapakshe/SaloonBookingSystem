using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Scheduling;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Security;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Infrastructure.Services;
using SalonBooking.Persistence.Context;

namespace SalonBooking.API.Tests;

public class SchedulingLogicTests
{
    private static readonly DateOnly AppointmentDate = new(2026, 10, 5);

    [Fact]
    public void Scheduling_state_follows_open_lines_only()
    {
        Assert.Equal(
            SchedulingStateCalculator.Unscheduled,
            SchedulingStateCalculator.Calculate([]));

        Assert.Equal(
            SchedulingStateCalculator.Unscheduled,
            SchedulingStateCalculator.Calculate([
                new ScheduleLineState(false, false),
                new ScheduleLineState(true, true)
            ]));

        Assert.Equal(
            SchedulingStateCalculator.PartiallyScheduled,
            SchedulingStateCalculator.Calculate([
                new ScheduleLineState(false, true),
                new ScheduleLineState(false, false)
            ]));

        Assert.Equal(
            SchedulingStateCalculator.Scheduled,
            SchedulingStateCalculator.Calculate([
                new ScheduleLineState(false, true),
                new ScheduleLineState(true, false)
            ]));

        Assert.True(SchedulingStateCalculator.NeedsScheduling(
            AppointmentStatus.Confirmed,
            SchedulingStateCalculator.Unscheduled));
        Assert.False(SchedulingStateCalculator.NeedsScheduling(
            AppointmentStatus.Completed,
            SchedulingStateCalculator.Unscheduled));
        Assert.False(SchedulingStateCalculator.NeedsScheduling(
            AppointmentStatus.Pending,
            SchedulingStateCalculator.Scheduled));
    }

    [Fact]
    public void Breaks_leave_and_bookings_are_removed_from_working_time()
    {
        var date = AppointmentDate;
        var working = new[] { new TimeRange(At(9), At(18)) };
        var withBreak = AvailabilityMath.Subtract(working, [new TimeRange(At(13), At(14))]);
        var slots = AvailabilityMath.Slots(withBreak, 30);

        Assert.Contains(slots, slot => slot.Start == At(9) && slot.End == At(9, 30));
        Assert.DoesNotContain(slots, slot => slot.Start == At(13));

        var withLeave = AvailabilityMath.Subtract(withBreak, [new TimeRange(At(10), At(12))]);
        Assert.False(AvailabilityMath.Fits(withLeave, At(10), At(10, 30)));
        Assert.True(AvailabilityMath.Fits(withLeave, At(9), At(9, 30)));

        var withBooking = AvailabilityMath.Subtract(withLeave, [new TimeRange(At(9), At(9, 30))]);
        Assert.False(AvailabilityMath.Fits(withBooking, At(9), At(9, 30)));
        Assert.True(AvailabilityMath.Fits(withBooking, At(14), At(14, 30)));
    }

    [Fact]
    public async Task Schedule_requires_capability_and_writes_history()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        var appointment = await scenario.Appointments.CreateAppointmentAsync(scenario.NewAppointment());
        var line = await scenario.Appointments.AddServiceAsync(
            appointment.AppointmentId,
            new AddAppointmentServiceRequest { ServiceId = scenario.ServiceId });

        await Assert.ThrowsAsync<SchedulingException>(() => scenario.Appointments.ScheduleServiceAsync(
            appointment.AppointmentId,
            line.AppointmentServiceId,
            scenario.Schedule(line.RowVersion, At(9), At(9, 30))));

        await scenario.Capabilities.AssignAsync(scenario.NewCapability());
        await scenario.Schedules.CreateAsync(scenario.Working(new TimeOnly(9, 0), new TimeOnly(18, 0)));

        var scheduled = await scenario.Appointments.ScheduleServiceAsync(
            appointment.AppointmentId,
            line.AppointmentServiceId,
            scenario.Schedule(line.RowVersion, At(9), At(9, 30), "First assignment"));

        Assert.True(scheduled.IsFullyScheduled);
        Assert.Equal(scenario.EmployeeId, scheduled.EmployeeId);
        var history = Assert.Single(scheduled.ScheduleHistory);
        Assert.Null(history.OldEmployeeId);
        Assert.Equal(scenario.EmployeeId, history.NewEmployeeId);
        Assert.Equal(At(9), history.NewStartTime);

        var loaded = await scenario.Appointments.GetAppointmentAsync(appointment.AppointmentId);
        Assert.Equal(SchedulingStateCalculator.Scheduled, loaded.SchedulingState);
        Assert.False(loaded.NeedsScheduling);
        Assert.Null(Assert.Single(loaded.StatusHistory).OldStatus);
        Assert.Equal(nameof(AppointmentStatus.Pending), loaded.StatusHistory[0].NewStatus);
    }

    [Fact]
    public async Task Availability_excludes_breaks_approved_leave_and_existing_bookings()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        await scenario.Capabilities.AssignAsync(scenario.NewCapability());
        await scenario.Schedules.CreateAsync(scenario.Working(new TimeOnly(9, 0), new TimeOnly(13, 0)));
        await scenario.Schedules.CreateAsync(scenario.Break(new TimeOnly(13, 0), new TimeOnly(14, 0)));
        await scenario.Schedules.CreateAsync(scenario.Working(new TimeOnly(14, 0), new TimeOnly(18, 0)));

        var open = await scenario.Availability.GetAvailabilityAsync(scenario.AvailabilityQuery());
        Assert.Contains(open[0].Slots, slot => slot.StartTime == At(9));
        Assert.DoesNotContain(open[0].Slots, slot => slot.StartTime == At(13));

        await scenario.Leave.CreateAsync(scenario.LeaveRequest(
            At(10), At(12), LeaveStatus.Pending));
        var pending = await scenario.Availability.GetAvailabilityAsync(scenario.AvailabilityQuery());
        Assert.Contains(pending[0].Slots, slot => slot.StartTime == At(10));

        var pendingLeave = Assert.Single(await scenario.Leave.GetAsync(
            new StaffLeaveQueryRequest { BranchId = scenario.BranchId }));
        await scenario.Leave.UpdateAsync(pendingLeave.StaffLeaveId, scenario.LeaveRequest(
            At(10), At(12), LeaveStatus.Approved));
        var approved = await scenario.Availability.GetAvailabilityAsync(scenario.AvailabilityQuery());
        Assert.DoesNotContain(approved[0].Slots, slot => slot.StartTime == At(10));
        Assert.Contains(approved[0].Slots, slot => slot.StartTime == At(9));

        var appointment = await scenario.Appointments.CreateAppointmentAsync(scenario.NewAppointment());
        var line = await scenario.Appointments.AddServiceAsync(
            appointment.AppointmentId,
            new AddAppointmentServiceRequest { ServiceId = scenario.ServiceId });
        await scenario.Appointments.ScheduleServiceAsync(
            appointment.AppointmentId,
            line.AppointmentServiceId,
            scenario.Schedule(line.RowVersion, At(9), At(9, 30)));

        var booked = await scenario.Availability.GetAvailabilityAsync(scenario.AvailabilityQuery());
        Assert.DoesNotContain(booked[0].Slots, slot => slot.StartTime == At(9));
    }

    [Fact]
    public async Task Overlapping_employee_booking_is_rejected()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        await scenario.Capabilities.AssignAsync(scenario.NewCapability());
        await scenario.Schedules.CreateAsync(scenario.Working(new TimeOnly(9, 0), new TimeOnly(18, 0)));
        var first = await scenario.ScheduleLineAsync(At(9), At(9, 30));

        var secondAppointment = await scenario.Appointments.CreateAppointmentAsync(scenario.NewAppointment());
        var second = await scenario.Appointments.AddServiceAsync(
            secondAppointment.AppointmentId,
            new AddAppointmentServiceRequest { ServiceId = scenario.ServiceId });

        var conflict = await Assert.ThrowsAsync<SchedulingConflictException>(() =>
            scenario.Appointments.ScheduleServiceAsync(
                secondAppointment.AppointmentId,
                second.AppointmentServiceId,
                scenario.Schedule(second.RowVersion, At(9, 15), At(9, 45))));

        Assert.Contains("not available", conflict.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(At(9), first.StartTime);
    }

    [Fact]
    public async Task Status_change_writes_history_and_cancel_releases_the_line()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        await scenario.Capabilities.AssignAsync(scenario.NewCapability());
        await scenario.Schedules.CreateAsync(scenario.Working(new TimeOnly(9, 0), new TimeOnly(18, 0)));
        var scheduled = await scenario.ScheduleLineAsync(At(9), At(9, 30));
        var appointment = await scenario.Appointments.ChangeStatusAsync(
            scheduled.AppointmentId,
            new ChangeAppointmentStatusRequest { Status = "Confirmed", Reason = "Customer confirmed" });

        Assert.Equal(nameof(AppointmentStatus.Confirmed), appointment.Status);
        Assert.Equal(2, appointment.StatusHistory.Count);
        Assert.Equal(nameof(AppointmentStatus.Pending), appointment.StatusHistory[1].OldStatus);
        Assert.Equal(nameof(AppointmentStatus.Confirmed), appointment.StatusHistory[1].NewStatus);

        var cancelled = await scenario.Appointments.CancelAppointmentAsync(
            appointment.AppointmentId,
            new CancelAppointmentRequest { Reason = "Customer cancelled" });

        Assert.Equal(nameof(AppointmentStatus.Cancelled), cancelled.Status);
        Assert.Equal("Customer cancelled", cancelled.CancellationReason);
        Assert.True(Assert.Single(cancelled.Services).IsCancelled);
        Assert.Equal(SchedulingStateCalculator.Unscheduled, cancelled.SchedulingState);

        var replacement = await scenario.ScheduleLineAsync(At(9), At(9, 30));
        Assert.True(replacement.IsFullyScheduled);
    }

    [Fact]
    public async Task Stale_row_version_does_not_overwrite_a_scheduled_line()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        await scenario.Capabilities.AssignAsync(scenario.NewCapability());
        await scenario.Schedules.CreateAsync(scenario.Working(new TimeOnly(9, 0), new TimeOnly(18, 0)));
        var scheduled = await scenario.ScheduleLineAsync(At(9), At(9, 30));

        var conflict = await Assert.ThrowsAsync<SchedulingConflictException>(() =>
            scenario.Appointments.ScheduleServiceAsync(
                scheduled.AppointmentId,
                scheduled.AppointmentServiceId,
                scenario.Schedule(Convert.ToBase64String(new byte[] { 9, 9, 9 }), At(10), At(10, 30))));

        Assert.Contains("changed by someone else", conflict.Message, StringComparison.OrdinalIgnoreCase);
        scenario.Db.ChangeTracker.Clear();
        var stored = await scenario.Db.AppointmentServices.SingleAsync();
        Assert.Equal(At(9), stored.StartTime);
        Assert.Equal(At(9, 30), stored.EndTime);
    }

    [Fact]
    public async Task Another_tenant_or_branch_cannot_read_the_appointment()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        var appointment = await scenario.Appointments.CreateAppointmentAsync(scenario.NewAppointment());

        await using var otherTenant = scenario.CreateContext(scenario.OtherTenantId, null);
        var otherTenantService = scenario.Workflow(otherTenant, scenario.OtherTenantId, null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            otherTenantService.GetAppointmentAsync(appointment.AppointmentId));

        await using var otherBranch = scenario.CreateContext(scenario.TenantId, scenario.OtherBranchId);
        var otherBranchService = scenario.Workflow(otherBranch, scenario.TenantId, scenario.OtherBranchId);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            otherBranchService.GetAppointmentAsync(appointment.AppointmentId));
    }

    [Fact]
    public async Task Customer_booking_stays_pending_until_the_salon_assigns_a_slot()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        var branch = await scenario.Db.Branches.SingleAsync(item => item.BranchId == scenario.BranchId);
        var service = await scenario.Db.Services.SingleAsync(item => item.ServiceId == scenario.ServiceId);
        var employee = await scenario.Db.Employees.SingleAsync(item => item.EmployeeId == scenario.EmployeeId);
        branch.IsCustomerVisible = true;
        service.IsCustomerVisible = true;
        employee.IsCustomerVisible = true;
        await scenario.Db.SaveChangesAsync();
        await scenario.Capabilities.AssignAsync(scenario.NewCapability());
        await scenario.Schedules.CreateAsync(scenario.Working(new TimeOnly(9, 0), new TimeOnly(18, 0)));
        var customerUser = new SchedulingUser(
            scenario.TenantId,
            scenario.BranchId,
            UserCategory.Customer,
            scenario.CustomerId);
        await using var customerDb = scenario.CreateContext(customerUser);
        var booking = new AppointmentWorkflowService(customerDb, customerUser);
        var preferred = AppointmentDate.ToDateTime(new TimeOnly(9, 0));

        var created = await booking.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = AppointmentDate,
            PreferredStartTime = preferred,
            Notes = "Requested stylist: Any available stylist"
        });

        Assert.Equal(nameof(AppointmentStatus.Pending), created.Status);
        Assert.Equal(SchedulingStateCalculator.Unscheduled, created.SchedulingState);
        var line = Assert.Single(created.Services);
        Assert.Null(line.StartTime);
        Assert.Null(line.EmployeeId);
        Assert.Equal(preferred, line.PreferredStartTime);

        var listed = await scenario.Appointments.GetAppointmentsAsync(new AppointmentQueryRequest
        {
            BranchId = scenario.BranchId,
            Date = AppointmentDate
        });
        Assert.Contains(listed.Items, item => item.AppointmentId == created.AppointmentId);

        await using var salonWideDb = scenario.CreateContext(scenario.TenantId, null);
        var salonWide = scenario.Workflow(salonWideDb, scenario.TenantId, null);
        var unscoped = await salonWide.GetAppointmentsAsync(new AppointmentQueryRequest());
        Assert.Contains(unscoped.Items, item => item.AppointmentId == created.AppointmentId);
    }

    [Fact]
    public async Task Customer_sees_only_their_own_appointments_and_status_changes()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        await PrepareCustomerBookingAsync(scenario);
        var other = new Customer
        {
            TenantId = scenario.TenantId,
            BranchId = scenario.BranchId,
            CustomerCode = "CUS000002",
            FirstName = "Other",
            LastName = "Guest",
            MobileNo = "0770000002"
        };
        scenario.Db.Customers.Add(other);
        await scenario.Db.SaveChangesAsync();

        var owner = CustomerWorkflow(scenario, scenario.CustomerId);
        var created = await owner.Service.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = AppointmentDate,
            PreferredStartTime = At(9)
        });

        var mine = await owner.Service.ListOwnAppointmentsAsync();
        var row = Assert.Single(mine);
        Assert.Equal(created.AppointmentId, row.AppointmentId);
        Assert.Equal(nameof(AppointmentStatus.Pending), row.Status);
        Assert.Equal("Branch", row.BranchName);
        Assert.Equal("Cut", row.ServiceName);
        Assert.Equal(At(9), row.PreferredStartTime);
        Assert.Null(row.ScheduledStartTime);
        Assert.Null(row.StylistName);
        Assert.Equal(CustomerCancellationRequest.None, row.CancellationRequest);

        var stranger = CustomerWorkflow(scenario, other.CustomerId);
        Assert.Empty(await stranger.Service.ListOwnAppointmentsAsync());
        await Assert.ThrowsAsync<SchedulingException>(() =>
            stranger.Service.RequestOwnCancellationAsync(created.AppointmentId));

        await using var otherTenantDb = scenario.CreateContext(new SchedulingUser(
            scenario.OtherTenantId,
            null,
            UserCategory.Customer,
            scenario.CustomerId));
        var otherTenant = new AppointmentWorkflowService(
            otherTenantDb,
            new SchedulingUser(scenario.OtherTenantId, null, UserCategory.Customer, scenario.CustomerId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => otherTenant.ListOwnAppointmentsAsync());

        var tooEarly = await Assert.ThrowsAsync<SchedulingException>(() =>
            scenario.Appointments.ChangeStatusAsync(
                created.AppointmentId,
                new ChangeAppointmentStatusRequest { Status = "Confirmed", Reason = "Slot assigned" }));
        Assert.Contains("Allocate every service", tooEarly.Message, StringComparison.OrdinalIgnoreCase);

        var advice = await scenario.Appointments.RecommendAllocationAsync(created.AppointmentId);
        await scenario.Appointments.ApplyAllocationAsync(created.AppointmentId, ToApply(advice.Options[0]));
        var stillPending = await scenario.Appointments.GetAppointmentAsync(created.AppointmentId);
        Assert.Equal(nameof(AppointmentStatus.Pending), stillPending.Status);

        var confirmed = await scenario.Appointments.ChangeStatusAsync(
            created.AppointmentId,
            new ChangeAppointmentStatusRequest { Status = "Confirmed", Reason = "Slot assigned" });
        Assert.Equal(nameof(AppointmentStatus.Confirmed), confirmed.Status);
        Assert.NotNull(Assert.Single(confirmed.Services).StartTime);

        owner.Db.ChangeTracker.Clear();
        var after = Assert.Single(await owner.Service.ListOwnAppointmentsAsync());
        Assert.Equal(nameof(AppointmentStatus.Confirmed), after.Status);
        Assert.NotNull(after.ScheduledStartTime);
        Assert.NotNull(after.PreferredStartTime);
    }

    [Fact]
    public async Task Customer_cancellation_waits_for_the_salon_and_rejects_duplicates()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        await PrepareCustomerBookingAsync(scenario);
        var owner = CustomerWorkflow(scenario, scenario.CustomerId);
        var day = UpcomingDate();
        var created = await owner.Service.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = day,
            PreferredStartTime = On(day, 10)
        });

        var requested = await owner.Service.RequestOwnCancellationAsync(created.AppointmentId);
        Assert.Equal(nameof(AppointmentStatus.Pending), requested.Status);
        Assert.Equal(CustomerCancellationRequest.Requested, requested.CancellationRequest);

        var duplicate = await Assert.ThrowsAsync<SchedulingException>(() =>
            owner.Service.RequestOwnCancellationAsync(created.AppointmentId));
        Assert.Contains("already waiting", duplicate.Message, StringComparison.OrdinalIgnoreCase);

        var declined = await scenario.Appointments.DeclineCancellationRequestAsync(created.AppointmentId);
        Assert.Equal(nameof(AppointmentStatus.Pending), declined.Status);
        Assert.Equal(CustomerCancellationRequest.Declined, declined.CancellationRequest);

        owner.Db.ChangeTracker.Clear();
        var afterDecline = Assert.Single(await owner.Service.ListOwnAppointmentsAsync());
        Assert.Equal(CustomerCancellationRequest.Declined, afterDecline.CancellationRequest);

        await owner.Service.RequestOwnCancellationAsync(created.AppointmentId);
        var cancelled = await scenario.Appointments.CancelAppointmentAsync(
            created.AppointmentId,
            new CancelAppointmentRequest { Reason = "Approved the customer's cancellation request" });
        Assert.Equal(nameof(AppointmentStatus.Cancelled), cancelled.Status);

        owner.Db.ChangeTracker.Clear();
        var afterCancel = Assert.Single(await owner.Service.ListOwnAppointmentsAsync());
        Assert.Equal(nameof(AppointmentStatus.Cancelled), afterCancel.Status);
        Assert.Equal(CustomerCancellationRequest.None, afterCancel.CancellationRequest);

        var closed = await Assert.ThrowsAsync<SchedulingException>(() =>
            owner.Service.RequestOwnCancellationAsync(created.AppointmentId));
        Assert.Contains("no longer", closed.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Approval_is_distinct_from_a_normal_staff_cancellation()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        await PrepareCustomerBookingAsync(scenario);
        var owner = CustomerWorkflow(scenario, scenario.CustomerId);
        var day = UpcomingDate();
        var requested = await owner.Service.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = day,
            PreferredStartTime = On(day, 11)
        });
        await owner.Service.RequestOwnCancellationAsync(requested.AppointmentId);

        var approved = await scenario.Appointments.ApproveCancellationRequestAsync(requested.AppointmentId);
        Assert.Equal(nameof(AppointmentStatus.Cancelled), approved.Status);
        Assert.Equal(CustomerCancellationRequest.Approved, approved.CancellationRequest);
        Assert.Equal(CustomerCancellationRequest.ApprovedReason, approved.CancellationReason);

        owner.Db.ChangeTracker.Clear();
        var listed = Assert.Single(await owner.Service.ListOwnAppointmentsAsync());
        Assert.Equal(CustomerCancellationRequest.Approved, listed.CancellationRequest);

        var again = await Assert.ThrowsAsync<SchedulingException>(() =>
            scenario.Appointments.ApproveCancellationRequestAsync(requested.AppointmentId));
        Assert.Contains("no longer", again.Message, StringComparison.OrdinalIgnoreCase);

        var plain = await owner.Service.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = day,
            PreferredStartTime = On(day, 12)
        });
        var staffCancelled = await scenario.Appointments.CancelAppointmentAsync(
            plain.AppointmentId,
            new CancelAppointmentRequest { Reason = "Salon closed" });
        Assert.Equal(nameof(AppointmentStatus.Cancelled), staffCancelled.Status);
        Assert.Equal(CustomerCancellationRequest.None, staffCancelled.CancellationRequest);
    }

    [Fact]
    public async Task Approved_cancellation_closes_open_lines_and_preserves_completed_work()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        await PrepareCustomerBookingAsync(scenario);
        var owner = CustomerWorkflow(scenario, scenario.CustomerId);
        var day = UpcomingDate();
        var openVisit = await owner.Service.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = day,
            PreferredStartTime = On(day, 9)
        });
        await owner.Service.RequestOwnCancellationAsync(openVisit.AppointmentId);

        var approved = await scenario.Appointments.ApproveCancellationRequestAsync(openVisit.AppointmentId);
        var openLine = Assert.Single(approved.Services);
        Assert.Equal(nameof(AppointmentStatus.Cancelled), approved.Status);
        Assert.Equal(nameof(ServiceLineStatus.Cancelled), openLine.LineStatus);
        Assert.True(openLine.IsCancelled);
        var openHistory = await scenario.Db.AppointmentServiceStatusHistories
            .Where(item => item.AppointmentServiceId == openLine.AppointmentServiceId)
            .OrderBy(item => item.AppointmentServiceStatusHistoryId)
            .ToListAsync();
        var cancelRow = Assert.Single(openHistory, item => item.Action == "Cancel");
        Assert.Equal(ServiceLineStatus.Pending, cancelRow.OldStatus);
        Assert.Equal(ServiceLineStatus.Cancelled, cancelRow.NewStatus);
        Assert.Equal(CustomerCancellationRequest.ApprovedReason, cancelRow.Reason);

        var repeated = await Assert.ThrowsAsync<SchedulingException>(() =>
            scenario.Appointments.ApproveCancellationRequestAsync(openVisit.AppointmentId));
        Assert.Contains("no longer", repeated.Message, StringComparison.OrdinalIgnoreCase);

        var mixed = await owner.Service.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = day,
            PreferredStartTime = On(day, 11),
            PreferredEmployeeId = scenario.EmployeeId
        });
        await scenario.Appointments.AddServiceAsync(
            mixed.AppointmentId,
            new AddAppointmentServiceRequest { ServiceId = scenario.ServiceId });
        var advice = await scenario.Appointments.RecommendAllocationAsync(mixed.AppointmentId);
        await scenario.Appointments.ApplyAllocationAsync(mixed.AppointmentId, ToApply(advice.Options[0]));
        var confirmed = await scenario.Appointments.ChangeStatusAsync(
            mixed.AppointmentId,
            new ChangeAppointmentStatusRequest { Status = "Confirmed" });
        await owner.Service.RequestOwnCancellationAsync(mixed.AppointmentId);
        var desk = await scenario.GrantUserAsync(
            null,
            scenario.BranchId,
            PermissionCatalog.AppointmentsLinesProgress);
        await using var deskDb = scenario.CreateContext(desk);
        var deskService = new AppointmentWorkflowService(deskDb, desk);
        var finished = confirmed.Services[0];
        var stillOpen = confirmed.Services[1];
        await deskService.ProgressOwnServiceAsync(
            mixed.AppointmentId,
            finished.AppointmentServiceId,
            new ProgressOwnServiceRequest { Action = "Start" });
        await deskService.ProgressOwnServiceAsync(
            mixed.AppointmentId,
            finished.AppointmentServiceId,
            new ProgressOwnServiceRequest { Action = "Finish" });
        scenario.Db.ChangeTracker.Clear();

        var closed = await scenario.Appointments.ApproveCancellationRequestAsync(mixed.AppointmentId);
        var kept = Assert.Single(closed.Services, line => line.AppointmentServiceId == finished.AppointmentServiceId);
        var stopped = Assert.Single(closed.Services, line => line.AppointmentServiceId == stillOpen.AppointmentServiceId);
        Assert.Equal(nameof(ServiceLineStatus.Completed), kept.LineStatus);
        Assert.False(kept.IsCancelled);
        Assert.Equal(finished.EmployeeId, kept.EmployeeId);
        Assert.Equal(nameof(ServiceLineStatus.Cancelled), stopped.LineStatus);
        Assert.True(stopped.IsCancelled);
        Assert.DoesNotContain(
            await scenario.Db.AppointmentServiceStatusHistories
                .Where(item => item.AppointmentServiceId == kept.AppointmentServiceId)
                .ToListAsync(),
            item => item.Action == "Cancel");
        var stoppedHistory = Assert.Single(
            await scenario.Db.AppointmentServiceStatusHistories
                .Where(item => item.AppointmentServiceId == stopped.AppointmentServiceId && item.Action == "Cancel")
                .ToListAsync());
        Assert.Equal(ServiceLineStatus.Assigned, stoppedHistory.OldStatus);
        Assert.Equal(ServiceLineStatus.Cancelled, stoppedHistory.NewStatus);

        var done = await owner.Service.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = day,
            PreferredStartTime = On(day, 15),
            PreferredEmployeeId = scenario.EmployeeId
        });
        var doneAdvice = await scenario.Appointments.RecommendAllocationAsync(done.AppointmentId);
        await scenario.Appointments.ApplyAllocationAsync(done.AppointmentId, ToApply(doneAdvice.Options[0]));
        var doneConfirmed = await scenario.Appointments.ChangeStatusAsync(
            done.AppointmentId,
            new ChangeAppointmentStatusRequest { Status = "Confirmed" });
        await owner.Service.RequestOwnCancellationAsync(done.AppointmentId);
        var onlyLine = Assert.Single(doneConfirmed.Services);
        await deskService.ProgressOwnServiceAsync(
            done.AppointmentId,
            onlyLine.AppointmentServiceId,
            new ProgressOwnServiceRequest { Action = "Start" });
        await deskService.ProgressOwnServiceAsync(
            done.AppointmentId,
            onlyLine.AppointmentServiceId,
            new ProgressOwnServiceRequest { Action = "Finish" });
        scenario.Db.ChangeTracker.Clear();

        var finishedVisit = await scenario.Appointments.GetAppointmentAsync(done.AppointmentId);
        Assert.Equal(nameof(AppointmentStatus.Completed), finishedVisit.Status);
        var cannotApprove = await Assert.ThrowsAsync<SchedulingException>(() =>
            scenario.Appointments.ApproveCancellationRequestAsync(done.AppointmentId));
        Assert.Contains("no longer", cannotApprove.Message, StringComparison.OrdinalIgnoreCase);
        var preserved = Assert.Single((await scenario.Appointments.GetAppointmentAsync(done.AppointmentId)).Services);
        Assert.Equal(nameof(ServiceLineStatus.Completed), preserved.LineStatus);
        Assert.False(preserved.IsCancelled);
        Assert.Equal(nameof(AppointmentStatus.Completed), (await scenario.Appointments.GetAppointmentAsync(done.AppointmentId)).Status);
    }

    [Fact]
    public async Task Cancellation_approval_rejects_invalid_visits_and_other_branches()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        await PrepareCustomerBookingAsync(scenario);
        var owner = CustomerWorkflow(scenario, scenario.CustomerId);
        var day = UpcomingDate();
        var created = await owner.Service.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = day,
            PreferredStartTime = On(day, 16)
        });

        var missing = await Assert.ThrowsAsync<SchedulingException>(() =>
            scenario.Appointments.ApproveCancellationRequestAsync(created.AppointmentId));
        Assert.Contains("no cancellation request", missing.Message, StringComparison.OrdinalIgnoreCase);

        await owner.Service.RequestOwnCancellationAsync(created.AppointmentId);
        await using var otherBranchDb = scenario.CreateContext(scenario.TenantId, scenario.OtherBranchId);
        var otherBranch = scenario.Workflow(otherBranchDb, scenario.TenantId, scenario.OtherBranchId);
        var blockedBranch = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            otherBranch.ApproveCancellationRequestAsync(created.AppointmentId));
        Assert.Contains("another branch", blockedBranch.Message, StringComparison.OrdinalIgnoreCase);

        await using var otherTenantDb = scenario.CreateContext(scenario.OtherTenantId, null);
        var otherTenant = scenario.Workflow(otherTenantDb, scenario.OtherTenantId, null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            otherTenant.ApproveCancellationRequestAsync(created.AppointmentId));

        var sameBranch = scenario.Workflow(
            scenario.CreateContext(scenario.TenantId, scenario.BranchId),
            scenario.TenantId,
            scenario.BranchId);
        var untouched = await scenario.Appointments.GetAppointmentAsync(created.AppointmentId);
        Assert.Equal(nameof(AppointmentStatus.Pending), untouched.Status);
        Assert.Equal(CustomerCancellationRequest.Requested, untouched.CancellationRequest);

        await scenario.Appointments.ChangeStatusAsync(
            created.AppointmentId,
            new ChangeAppointmentStatusRequest { Status = "NoShow", Reason = "Customer did not arrive" });
        var terminal = await Assert.ThrowsAsync<SchedulingException>(() =>
            sameBranch.ApproveCancellationRequestAsync(created.AppointmentId));
        Assert.Contains("no longer", terminal.Message, StringComparison.OrdinalIgnoreCase);
        var noShow = await scenario.Appointments.GetAppointmentAsync(created.AppointmentId);
        Assert.Equal(nameof(AppointmentStatus.NoShow), noShow.Status);
        Assert.Equal(nameof(ServiceLineStatus.Cancelled), Assert.Single(noShow.Services).LineStatus);
        Assert.True(Assert.Single(noShow.Services).IsCancelled);
    }

    [Fact]
    public async Task Decline_and_unrequested_approval_keep_the_visit_open()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        await PrepareCustomerBookingAsync(scenario);
        var owner = CustomerWorkflow(scenario, scenario.CustomerId);
        var day = UpcomingDate();
        var created = await owner.Service.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = day,
            PreferredStartTime = On(day, 13)
        });

        var missing = await Assert.ThrowsAsync<SchedulingException>(() =>
            scenario.Appointments.ApproveCancellationRequestAsync(created.AppointmentId));
        Assert.Contains("no cancellation request", missing.Message, StringComparison.OrdinalIgnoreCase);

        await owner.Service.RequestOwnCancellationAsync(created.AppointmentId);
        var declined = await scenario.Appointments.DeclineCancellationRequestAsync(created.AppointmentId);
        Assert.Equal(nameof(AppointmentStatus.Pending), declined.Status);
        Assert.Equal(CustomerCancellationRequest.Declined, declined.CancellationRequest);

        var afterDecline = await Assert.ThrowsAsync<SchedulingException>(() =>
            scenario.Appointments.ApproveCancellationRequestAsync(created.AppointmentId));
        Assert.Contains("no cancellation request", afterDecline.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Customer_cannot_cancel_an_appointment_after_its_salon_date()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        await PrepareCustomerBookingAsync(scenario);
        var owner = CustomerWorkflow(scenario, scenario.CustomerId);
        var created = await owner.Service.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = AppointmentDate,
            PreferredStartTime = At(16)
        });

        var rejected = await Assert.ThrowsAsync<SchedulingException>(() =>
            owner.Service.RequestOwnCancellationAsync(created.AppointmentId));
        Assert.Contains("passed", rejected.Message, StringComparison.OrdinalIgnoreCase);

        var listed = Assert.Single(await owner.Service.ListOwnAppointmentsAsync());
        Assert.Equal(nameof(AppointmentStatus.Pending), listed.Status);
        Assert.Equal(CustomerCancellationRequest.None, listed.CancellationRequest);

        var staffCancelled = await scenario.Appointments.CancelAppointmentAsync(
            created.AppointmentId,
            new CancelAppointmentRequest { Reason = "Salon closed" });
        Assert.Equal(nameof(AppointmentStatus.Cancelled), staffCancelled.Status);
        Assert.Equal(CustomerCancellationRequest.None, staffCancelled.CancellationRequest);
    }

    [Fact]
    public async Task Simultaneous_cancellation_requests_accept_only_one()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        await PrepareCustomerBookingAsync(scenario);
        var user = new SchedulingUser(scenario.TenantId, scenario.BranchId, UserCategory.Customer, scenario.CustomerId);
        await using var firstDb = scenario.CreateSeparateConnection(user);
        await using var secondDb = scenario.CreateSeparateConnection(user);
        var first = new AppointmentWorkflowService(firstDb, user);
        var day = UpcomingDate();
        var created = await first.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = day,
            PreferredStartTime = On(day, 14)
        });
        var second = new AppointmentWorkflowService(secondDb, user);

        var outcomes = await Task.WhenAll(
            Attempt(first, created.AppointmentId),
            Attempt(second, created.AppointmentId));

        Assert.Equal(1, outcomes.Count(item => item == null));
        Assert.Contains(outcomes, item => item != null && item.Contains("already waiting", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Pending_cancellation_queue_hides_staff_cancels_and_other_branches()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        await PrepareCustomerBookingAsync(scenario);
        var owner = CustomerWorkflow(scenario, scenario.CustomerId);
        var day = UpcomingDate();
        var requested = await owner.Service.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = day,
            PreferredStartTime = On(day, 10)
        });
        await owner.Service.RequestOwnCancellationAsync(requested.AppointmentId);

        var staffVisit = await scenario.Appointments.CreateAppointmentAsync(new CreateAppointmentRequest
        {
            BranchId = scenario.BranchId,
            CustomerId = scenario.CustomerId,
            AppointmentDate = day
        });
        var cancelled = await scenario.Appointments.CancelAppointmentAsync(
            staffVisit.AppointmentId,
            new CancelAppointmentRequest { Reason = "Salon closed" });
        Assert.Equal(CustomerCancellationRequest.None, cancelled.CancellationRequest);

        var queue = await scenario.Appointments.ListStaffNotificationsAsync();
        var notice = Assert.Single(queue);
        Assert.Equal("CancellationRequest", notice.NotificationType);
        Assert.Equal(requested.AppointmentId, notice.AppointmentId);
        Assert.Equal("Ada Lovelace", notice.CustomerName);
        Assert.Equal("Cut", notice.ServiceName);
        Assert.Equal("Branch", notice.BranchName);
        Assert.Equal(CustomerCancellationRequest.Requested, notice.RequestState);
        Assert.Equal(On(day, 10), notice.AppointmentTime);
        Assert.DoesNotContain(queue, item => item.AppointmentId == staffVisit.AppointmentId);
        Assert.DoesNotContain("077", notice.CustomerName, StringComparison.Ordinal);

        var bookingsOnly = await scenario.OpenAsAsync(PermissionCatalog.NotificationsBookingsRead);
        Assert.Empty(await bookingsOnly.ListStaffNotificationsAsync());
        var cancellationsOnly = await scenario.OpenAsAsync(PermissionCatalog.NotificationsCancellationsRead);
        Assert.Equal(requested.AppointmentId, Assert.Single(await cancellationsOnly.ListStaffNotificationsAsync()).AppointmentId);

        var otherBranch = scenario.Workflow(
            scenario.CreateContext(scenario.TenantId, scenario.OtherBranchId),
            scenario.TenantId,
            scenario.OtherBranchId);
        Assert.Empty(await otherBranch.ListStaffNotificationsAsync());

        var otherTenant = scenario.Workflow(
            scenario.CreateContext(scenario.OtherTenantId, null),
            scenario.OtherTenantId,
            null);
        Assert.Empty(await otherTenant.ListStaffNotificationsAsync());

        await scenario.Appointments.DeclineCancellationRequestAsync(requested.AppointmentId);
        var afterDecline = await scenario.Appointments.ListStaffNotificationsAsync();
        Assert.Equal(CustomerBookingRequest.NotificationType, Assert.Single(afterDecline).NotificationType);

        await owner.Service.RequestOwnCancellationAsync(requested.AppointmentId);
        await scenario.Appointments.ApproveCancellationRequestAsync(requested.AppointmentId);
        Assert.Empty(await scenario.Appointments.ListStaffNotificationsAsync());
    }

    [Fact]
    public async Task Unallocated_customer_booking_stays_until_it_is_scheduled()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        await PrepareCustomerBookingAsync(scenario);
        var owner = CustomerWorkflow(scenario, scenario.CustomerId);
        var day = UpcomingDate();
        var created = await owner.Service.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = day,
            PreferredStartTime = On(day, 10),
            PreferredEmployeeId = scenario.EmployeeId
        });

        var before = await scenario.Appointments.ListStaffNotificationsAsync();
        var notice = Assert.Single(before);
        Assert.Equal(CustomerBookingRequest.NotificationType, notice.NotificationType);
        Assert.Equal(CustomerBookingRequest.Unallocated, notice.AllocationState);
        Assert.Equal("Grace", notice.PreferredStylistName);
        Assert.Equal(On(day, 10), notice.AppointmentTime);

        var again = await scenario.Appointments.ListStaffNotificationsAsync();
        Assert.Equal(created.AppointmentId, Assert.Single(again).AppointmentId);
        var advice = await scenario.Appointments.RecommendAllocationAsync(created.AppointmentId);
        Assert.Equal(created.AppointmentId, Assert.Single(await scenario.Appointments.ListStaffNotificationsAsync()).AppointmentId);

        var best = advice.Options[0];
        Assert.True(best.ExactMatch);
        Assert.True(best.UsesPreferredStylist);
        Assert.Equal(On(day, 10), best.Lines[0].StartTime);
        Assert.Equal(30, best.Lines[0].DurationMinutes);

        await scenario.Appointments.ApplyAllocationAsync(created.AppointmentId, ToApply(best));
        var afterAllocation = Assert.Single(await scenario.Appointments.ListStaffNotificationsAsync());
        Assert.Equal(CustomerBookingRequest.AwaitingConfirmation, afterAllocation.NotificationType);
        var saved = await scenario.Appointments.GetAppointmentAsync(created.AppointmentId);
        Assert.Equal(nameof(AppointmentStatus.Pending), saved.Status);
        Assert.Equal(On(day, 10), Assert.Single(saved.Services).StartTime);
    }

    [Fact]
    public async Task Recommendations_skip_a_taken_slot_and_reject_a_stale_save()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        await PrepareCustomerBookingAsync(scenario);
        var day = UpcomingDate();
        var owner = CustomerWorkflow(scenario, scenario.CustomerId);
        var created = await owner.Service.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = day,
            PreferredStartTime = On(day, 10)
        });
        var blocker = await scenario.Appointments.CreateAppointmentAsync(new CreateAppointmentRequest
        {
            BranchId = scenario.BranchId,
            CustomerId = scenario.CustomerId,
            AppointmentDate = day
        });
        var blockerLine = await scenario.Appointments.AddServiceAsync(
            blocker.AppointmentId,
            new AddAppointmentServiceRequest { ServiceId = scenario.ServiceId });
        await scenario.Appointments.ScheduleServiceAsync(
            blocker.AppointmentId,
            blockerLine.AppointmentServiceId,
            scenario.Schedule(blockerLine.RowVersion, On(day, 10), On(day, 10).AddMinutes(30)));
        var advice = await scenario.Appointments.RecommendAllocationAsync(created.AppointmentId);
        Assert.NotEmpty(advice.Options);
        Assert.All(advice.Options, option => Assert.False(option.ExactMatch));
        Assert.Equal(0, advice.Options[0].DayDifference);
        Assert.True(advice.Options[0].MinuteDifference > 0);
        Assert.DoesNotContain(advice.Options, option => option.Lines.Any(line => line.StartTime == On(day, 10)));

        var outsider = new Employee
        {
            TenantId = scenario.TenantId,
            BranchId = scenario.BranchId,
            EmployeeCode = "EMP000009",
            FirstName = "Outside",
            LastName = "Skill",
            MobileNo = "0710000009",
            IsActive = true
        };
        scenario.Db.Employees.Add(outsider);
        await scenario.Db.SaveChangesAsync();
        Assert.DoesNotContain(advice.Options, option => option.Lines.Any(line => line.EmployeeId == outsider.EmployeeId));

        var best = advice.Options[0];
        await scenario.Appointments.ApplyAllocationAsync(created.AppointmentId, ToApply(best));
        var stale = await Assert.ThrowsAsync<SchedulingConflictException>(() =>
            scenario.Appointments.ApplyAllocationAsync(created.AppointmentId, ToApply(best)));
        Assert.Contains("no longer available", stale.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Multi_service_recommendation_keeps_each_duration_in_sequence()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        await PrepareCustomerBookingAsync(scenario);
        var colour = new Service
        {
            TenantId = scenario.TenantId,
            BranchId = scenario.BranchId,
            ServiceCategoryId = scenario.Db.Services.Single(item => item.ServiceId == scenario.ServiceId).ServiceCategoryId,
            ServiceCode = "SVC9",
            ServiceName = "Colour",
            DurationMinutes = 45,
            Price = 40,
            IsCustomerVisible = true
        };
        scenario.Db.Services.Add(colour);
        await scenario.Db.SaveChangesAsync();
        await scenario.Capabilities.AssignAsync(new AssignEmployeeCapabilityRequest
        {
            BranchId = scenario.BranchId,
            EmployeeId = scenario.EmployeeId,
            ServiceId = colour.ServiceId,
            PreferenceOrder = 1
        });
        var owner = CustomerWorkflow(scenario, scenario.CustomerId);
        var day = UpcomingDate();
        var created = await owner.Service.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = day,
            PreferredStartTime = On(day, 11)
        });
        await scenario.Appointments.AddServiceAsync(created.AppointmentId, new AddAppointmentServiceRequest
        {
            ServiceId = colour.ServiceId
        });

        var advice = await scenario.Appointments.RecommendAllocationAsync(created.AppointmentId);
        var best = advice.Options[0];
        Assert.True(best.ExactMatch);
        Assert.Equal(2, best.Lines.Count);
        Assert.Equal(30, best.Lines[0].DurationMinutes);
        Assert.Equal(45, best.Lines[1].DurationMinutes);
        Assert.Equal(best.Lines[0].EndTime, best.Lines[1].StartTime);
        Assert.Equal(new[] { "Cut", "Colour" }, advice.ServiceNames);
    }

    [Fact]
    public async Task Past_pending_requests_stay_out_of_the_notification_queue()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        await PrepareCustomerBookingAsync(scenario);
        var owner = CustomerWorkflow(scenario, scenario.CustomerId);
        var past = SalonTime.Today().AddDays(-7);
        var created = await owner.Service.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = past,
            PreferredStartTime = On(past, 10)
        });

        Assert.DoesNotContain(
            await scenario.Appointments.ListStaffNotificationsAsync(),
            item => item.AppointmentId == created.AppointmentId);

        var advice = await scenario.Appointments.RecommendAllocationAsync(created.AppointmentId);
        await scenario.Appointments.ApplyAllocationAsync(created.AppointmentId, ToApply(advice.Options[0]));
        Assert.DoesNotContain(
            await scenario.Appointments.ListStaffNotificationsAsync(),
            item => item.AppointmentId == created.AppointmentId &&
                    item.NotificationType == CustomerBookingRequest.AwaitingConfirmation);
    }

    [Fact]
    public async Task Allocated_future_visit_waits_for_confirmation_and_stays_on_one_employee()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        await PrepareCustomerBookingAsync(scenario);
        var owner = CustomerWorkflow(scenario, scenario.CustomerId);
        var day = UpcomingDate();
        var created = await owner.Service.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = day,
            PreferredStartTime = On(day, 11),
            PreferredEmployeeId = scenario.EmployeeId
        });
        await scenario.Appointments.AddServiceAsync(
            created.AppointmentId,
            new AddAppointmentServiceRequest { ServiceId = scenario.ServiceId });

        var partial = await Assert.ThrowsAsync<SchedulingException>(() =>
            scenario.Appointments.ChangeStatusAsync(
                created.AppointmentId,
                new ChangeAppointmentStatusRequest { Status = "Confirmed" }));
        Assert.Contains("Allocate every service", partial.Message, StringComparison.OrdinalIgnoreCase);

        var advice = await scenario.Appointments.RecommendAllocationAsync(created.AppointmentId);
        var best = advice.Options[0];
        Assert.Equal(2, best.Lines.Count);
        await scenario.Appointments.ApplyAllocationAsync(created.AppointmentId, ToApply(best));
        var allocated = await scenario.Appointments.GetAppointmentAsync(created.AppointmentId);
        Assert.Equal(nameof(AppointmentStatus.Pending), allocated.Status);

        var waiting = await scenario.Appointments.ListStaffNotificationsAsync();
        var notice = Assert.Single(waiting, item => item.AppointmentId == created.AppointmentId);
        Assert.Equal(CustomerBookingRequest.AwaitingConfirmation, notice.NotificationType);
        Assert.DoesNotContain(waiting, item =>
            item.AppointmentId == created.AppointmentId &&
            item.NotificationType == CustomerBookingRequest.NotificationType);

        var confirmed = await scenario.Appointments.ChangeStatusAsync(
            created.AppointmentId,
            new ChangeAppointmentStatusRequest { Status = "Confirmed", Reason = "Bridal party confirmed" });
        Assert.Equal(nameof(AppointmentStatus.Confirmed), confirmed.Status);
        Assert.DoesNotContain(
            await scenario.Appointments.ListStaffNotificationsAsync(),
            item => item.AppointmentId == created.AppointmentId &&
                    item.NotificationType == CustomerBookingRequest.AwaitingConfirmation);

        await using var stylistDb = scenario.CreateContext(scenario.TenantId, scenario.BranchId);
        var stylist = new AppointmentWorkflowService(
            stylistDb,
            new SchedulingUser(scenario.TenantId, scenario.BranchId, employeeId: scenario.EmployeeId));
        var mine = await stylist.ListMyAssignedAppointmentsAsync();
        var own = Assert.Single(mine);
        Assert.Equal(created.AppointmentId, own.AppointmentId);
        Assert.All(own.Services, line => Assert.Equal(scenario.EmployeeId, line.EmployeeId));
        Assert.Equal("Ada Lovelace", own.CustomerName);
        Assert.All(own.Services, line => Assert.Equal("Cut", line.ServiceName));
        Assert.All(own.Services, line => Assert.Equal(0, line.UnitPrice));
        Assert.Empty(own.StatusHistory);

        var beforeStatus = (await scenario.Appointments.GetAppointmentAsync(created.AppointmentId)).Status;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            stylist.ProgressOwnServiceAsync(
                created.AppointmentId,
                own.Services[0].AppointmentServiceId,
                new ProgressOwnServiceRequest { Action = "Commence" }));

        var stranger = new AppointmentWorkflowService(
            stylistDb,
            new SchedulingUser(scenario.TenantId, scenario.BranchId, employeeId: scenario.EmployeeId + 50));
        Assert.Empty(await stranger.ListMyAssignedAppointmentsAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            stranger.ProgressOwnServiceAsync(
                created.AppointmentId,
                own.Services[0].AppointmentServiceId,
                new ProgressOwnServiceRequest { Action = "Complete" }));
        Assert.Equal(beforeStatus, (await scenario.Appointments.GetAppointmentAsync(created.AppointmentId)).Status);
        await Assert.ThrowsAsync<SchedulingException>(() =>
            scenario.Appointments.ListMyAssignedAppointmentsAsync());

        await using var otherBranch = scenario.CreateContext(scenario.TenantId, scenario.OtherBranchId);
        var other = scenario.Workflow(otherBranch, scenario.TenantId, scenario.OtherBranchId);
        Assert.Empty(await other.ListStaffNotificationsAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            other.ListStaffNotificationsAsync(branchId: scenario.BranchId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            scenario.Appointments.ListStaffNotificationsAsync(branchId: 999999));
    }

    [Fact]
    public async Task Service_lines_progress_independently_and_keep_history()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        await PrepareCustomerBookingAsync(scenario);
        var owner = CustomerWorkflow(scenario, scenario.CustomerId);
        var day = UpcomingDate();
        var created = await owner.Service.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = day,
            PreferredStartTime = On(day, 11),
            PreferredEmployeeId = scenario.EmployeeId
        });
        await scenario.Appointments.AddServiceAsync(
            created.AppointmentId,
            new AddAppointmentServiceRequest { ServiceId = scenario.ServiceId });
        var advice = await scenario.Appointments.RecommendAllocationAsync(created.AppointmentId);
        await scenario.Appointments.ApplyAllocationAsync(created.AppointmentId, ToApply(advice.Options[0]));
        var confirmed = await scenario.Appointments.ChangeStatusAsync(
            created.AppointmentId,
            new ChangeAppointmentStatusRequest { Status = "Confirmed" });
        Assert.Equal(nameof(AppointmentStatus.Confirmed), confirmed.Status);
        Assert.All(confirmed.Services, line => Assert.Equal(nameof(ServiceLineStatus.Assigned), line.LineStatus));

        var stylistUser = await scenario.GrantUserAsync(
            scenario.EmployeeId,
            scenario.BranchId,
            PermissionCatalog.AppointmentsOwnRead,
            PermissionCatalog.AppointmentsOwnProgress);
        await using var stylistDb = scenario.CreateContext(stylistUser);
        var stylist = new AppointmentWorkflowService(stylistDb, stylistUser);
        var past = new Appointment
        {
            TenantId = scenario.TenantId,
            BranchId = scenario.BranchId,
            CustomerId = scenario.CustomerId,
            AppointmentDate = SalonTime.Today().AddDays(-2),
            Status = AppointmentStatus.Completed
        };
        scenario.Db.Appointments.Add(past);
        await scenario.Db.SaveChangesAsync();
        scenario.Db.AppointmentServices.Add(new AppointmentService
        {
            TenantId = scenario.TenantId,
            BranchId = scenario.BranchId,
            AppointmentId = past.AppointmentId,
            ServiceId = scenario.ServiceId,
            DurationMinutes = 30,
            UnitPrice = 25,
            Sequence = 1,
            EmployeeId = scenario.EmployeeId,
            StartTime = past.AppointmentDate.ToDateTime(new TimeOnly(10, 0)),
            EndTime = past.AppointmentDate.ToDateTime(new TimeOnly(10, 30)),
            LineStatus = ServiceLineStatus.Completed,
            RowVersion = Guid.NewGuid().ToByteArray()
        });
        await scenario.Db.SaveChangesAsync();
        var mine = await stylist.ListMyAssignedAppointmentsAsync();
        Assert.Contains(mine, item => item.AppointmentId == created.AppointmentId);
        Assert.Contains(mine, item => item.AppointmentId == past.AppointmentId);
        var first = confirmed.Services[0];
        var second = confirmed.Services[1];
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            stylist.ProgressOwnServiceAsync(
                created.AppointmentId,
                first.AppointmentServiceId,
                new ProgressOwnServiceRequest { Action = "Commence", Reason = "Customer is seated" }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            stylist.ProgressOwnServiceAsync(
                created.AppointmentId,
                first.AppointmentServiceId,
                new ProgressOwnServiceRequest { Action = "Complete" }));
        var desk = await scenario.GrantUserAsync(
            null,
            scenario.BranchId,
            PermissionCatalog.AppointmentsLinesProgress);
        await using var deskDb = scenario.CreateContext(desk);
        var deskService = new AppointmentWorkflowService(deskDb, desk);
        var startedAt = DateTime.UtcNow.AddSeconds(-1);
        var started = await deskService.ProgressOwnServiceAsync(
            created.AppointmentId,
            first.AppointmentServiceId,
            new ProgressOwnServiceRequest { Action = "Commence", Reason = "Customer is seated" });
        Assert.Equal(nameof(ServiceLineStatus.InProgress), started.LineStatus);
        var startedHistory = await deskService.ListLineProgressHistoryAsync(
            created.AppointmentId,
            first.AppointmentServiceId);
        var startRow = Assert.Single(startedHistory, item => item.Action == "Start");
        Assert.Equal(desk.UserId, startRow.ChangedByUserId);
        Assert.InRange(startRow.ChangedAt, startedAt, DateTime.UtcNow.AddSeconds(5));
        scenario.Db.ChangeTracker.Clear();
        var afterStart = await scenario.Appointments.GetAppointmentAsync(created.AppointmentId);
        Assert.Equal(nameof(AppointmentStatus.InProgress), afterStart.Status);
        Assert.Equal(nameof(ServiceLineStatus.Assigned), afterStart.Services[1].LineStatus);

        var finished = await deskService.ProgressOwnServiceAsync(
            created.AppointmentId,
            first.AppointmentServiceId,
            new ProgressOwnServiceRequest { Action = "Complete" });
        Assert.Equal(nameof(ServiceLineStatus.Completed), finished.LineStatus);
        scenario.Db.ChangeTracker.Clear();
        var oneDone = await scenario.Appointments.GetAppointmentAsync(created.AppointmentId);
        Assert.Equal(nameof(AppointmentStatus.InProgress), oneDone.Status);
        Assert.Equal(nameof(ServiceLineStatus.Assigned), oneDone.Services[1].LineStatus);

        var outsider = new Employee
        {
            TenantId = scenario.TenantId,
            BranchId = scenario.BranchId,
            EmployeeCode = "EMP000099",
            FirstName = "Other",
            LastName = "Stylist",
            MobileNo = "0710000099"
        };
        scenario.Db.Employees.Add(outsider);
        await scenario.Db.SaveChangesAsync();
        var stranger = await scenario.GrantUserAsync(
            outsider.EmployeeId,
            scenario.BranchId,
            PermissionCatalog.AppointmentsOwnProgress);
        await using var strangerDb = scenario.CreateContext(stranger);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new AppointmentWorkflowService(strangerDb, stranger).ProgressOwnServiceAsync(
                created.AppointmentId,
                second.AppointmentServiceId,
                new ProgressOwnServiceRequest { Action = "Commence" }));

        var managerOnly = await scenario.GrantUserAsync(
            null,
            scenario.BranchId,
            PermissionCatalog.AppointmentsManage,
            PermissionCatalog.AppointmentsRead);
        await using var managerDb = scenario.CreateContext(managerOnly);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new AppointmentWorkflowService(managerDb, managerOnly).ProgressOwnServiceAsync(
                created.AppointmentId,
                second.AppointmentServiceId,
                new ProgressOwnServiceRequest { Action = "Commence" }));

        var supervisor = await scenario.GrantUserAsync(
            null,
            scenario.BranchId,
            PermissionCatalog.AppointmentsRead,
            PermissionCatalog.SchedulesRead,
            PermissionCatalog.AppointmentsLinesProgress);
        await using var supervisorDb = scenario.CreateContext(supervisor);
        var supervisorService = new AppointmentWorkflowService(supervisorDb, supervisor);
        var otherBranchUser = await scenario.GrantUserAsync(
            null,
            scenario.OtherBranchId,
            PermissionCatalog.AppointmentsLinesProgress);
        await using var otherBranchDb = scenario.CreateContext(otherBranchUser);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new AppointmentWorkflowService(otherBranchDb, otherBranchUser).ProgressOwnServiceAsync(
                created.AppointmentId,
                second.AppointmentServiceId,
                new ProgressOwnServiceRequest { Action = "Commence" }));

        var otherEmployee = new Employee
        {
            TenantId = scenario.TenantId,
            BranchId = scenario.BranchId,
            EmployeeCode = "EMP000002",
            FirstName = "Ada",
            LastName = "Lovelace",
            MobileNo = "0710000002"
        };
        scenario.Db.Employees.Add(otherEmployee);
        await scenario.Db.SaveChangesAsync();
        var capability = scenario.NewCapability();
        capability.EmployeeId = otherEmployee.EmployeeId;
        await scenario.Capabilities.AssignAsync(capability);
        var hours = scenario.Working(new TimeOnly(9, 0), new TimeOnly(18, 0));
        hours.EmployeeId = otherEmployee.EmployeeId;
        hours.DayOfWeek = (byte)day.DayOfWeek;
        await scenario.Schedules.CreateAsync(hours);
        var beforeReassign = await supervisorService.ListLineProgressHistoryAsync(
            created.AppointmentId,
            second.AppointmentServiceId);
        var assignCount = beforeReassign.Count(item => item.Action == "Assign");
        scenario.Db.ChangeTracker.Clear();
        var current = (await scenario.Appointments.GetAppointmentAsync(created.AppointmentId))
            .Services.Single(line => line.AppointmentServiceId == second.AppointmentServiceId);
        var move = scenario.Schedule(current.RowVersion, On(day, 16), On(day, 16, 30), "Moved to Ada");
        move.EmployeeId = otherEmployee.EmployeeId;
        await scenario.Appointments.ScheduleServiceAsync(
            created.AppointmentId,
            second.AppointmentServiceId,
            move);
        var afterReassign = await supervisorService.ListLineProgressHistoryAsync(
            created.AppointmentId,
            second.AppointmentServiceId);
        Assert.Equal(assignCount, afterReassign.Count(item => item.Action == "Assign"));
        Assert.Contains(afterReassign, item => item.Action == "Reassign" && item.EmployeeId == otherEmployee.EmployeeId);

        var stale = await Assert.ThrowsAsync<SchedulingConflictException>(() =>
            supervisorService.ProgressOwnServiceAsync(
                created.AppointmentId,
                second.AppointmentServiceId,
                new ProgressOwnServiceRequest { Action = "Start", RowVersion = current.RowVersion }));
        Assert.Contains("changed by someone else", stale.Message, StringComparison.OrdinalIgnoreCase);

        scenario.Db.ChangeTracker.Clear();
        current = (await scenario.Appointments.GetAppointmentAsync(created.AppointmentId))
            .Services.Single(line => line.AppointmentServiceId == second.AppointmentServiceId);
        await using var supervisorAfterConflict = scenario.CreateContext(supervisor);
        var supervisorRetry = new AppointmentWorkflowService(supervisorAfterConflict, supervisor);
        var startedBySupervisor = await supervisorRetry.ProgressOwnServiceAsync(
            created.AppointmentId,
            second.AppointmentServiceId,
            new ProgressOwnServiceRequest { Action = "Start", RowVersion = current.RowVersion });
        Assert.Equal(nameof(ServiceLineStatus.InProgress), startedBySupervisor.LineStatus);
        var duplicate = await Assert.ThrowsAsync<SchedulingException>(() =>
            supervisorRetry.ProgressOwnServiceAsync(
                created.AppointmentId,
                second.AppointmentServiceId,
                new ProgressOwnServiceRequest { Action = "Start" }));
        Assert.Contains("already in progress", duplicate.Message, StringComparison.OrdinalIgnoreCase);
        var history = await stylist.ListLineProgressHistoryAsync(created.AppointmentId, first.AppointmentServiceId);
        Assert.Contains(history, item => item.Action == "Start" && item.ChangedByUserId == desk.UserId);
        Assert.Equal(1, history.Count(item => item.Action == "Start"));

        await supervisorRetry.ProgressOwnServiceAsync(
            created.AppointmentId,
            second.AppointmentServiceId,
            new ProgressOwnServiceRequest { Action = "Finish" });
        scenario.Db.ChangeTracker.Clear();
        var done = await scenario.Appointments.GetAppointmentAsync(created.AppointmentId);
        Assert.Equal(nameof(AppointmentStatus.Completed), done.Status);
        await using var closedDb = scenario.CreateContext(desk);
        var restart = await Assert.ThrowsAsync<SchedulingException>(() =>
            new AppointmentWorkflowService(closedDb, desk).ProgressOwnServiceAsync(
                created.AppointmentId,
                first.AppointmentServiceId,
                new ProgressOwnServiceRequest { Action = "Commence" }));
        Assert.Contains("closed appointment", restart.Message, StringComparison.OrdinalIgnoreCase);

        var followUp = await owner.Service.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = day,
            PreferredStartTime = On(day, 17),
            PreferredEmployeeId = scenario.EmployeeId
        });
        await scenario.Appointments.AddServiceAsync(
            followUp.AppointmentId,
            new AddAppointmentServiceRequest { ServiceId = scenario.ServiceId });
        var followAdvice = await scenario.Appointments.RecommendAllocationAsync(followUp.AppointmentId);
        await scenario.Appointments.ApplyAllocationAsync(followUp.AppointmentId, ToApply(followAdvice.Options[0]));
        await scenario.Appointments.ChangeStatusAsync(
            followUp.AppointmentId,
            new ChangeAppointmentStatusRequest { Status = "Confirmed" });
        scenario.Db.ChangeTracker.Clear();
        var followLines = (await scenario.Appointments.GetAppointmentAsync(followUp.AppointmentId)).Services;
        await deskService.ProgressOwnServiceAsync(
            followUp.AppointmentId,
            followLines[0].AppointmentServiceId,
            new ProgressOwnServiceRequest { Action = "Start" });
        await deskService.ProgressOwnServiceAsync(
            followUp.AppointmentId,
            followLines[0].AppointmentServiceId,
            new ProgressOwnServiceRequest { Action = "Finish" });
        scenario.Db.ChangeTracker.Clear();
        var openFollow = await scenario.Appointments.GetAppointmentAsync(followUp.AppointmentId);
        var cannotCancelDone = await Assert.ThrowsAsync<SchedulingException>(() =>
            scenario.Appointments.CancelServiceAsync(
                followUp.AppointmentId,
                openFollow.Services[0].AppointmentServiceId,
                openFollow.Services[0].RowVersion));
        Assert.Contains("completed service", cannotCancelDone.Message, StringComparison.OrdinalIgnoreCase);
        await scenario.Appointments.CancelServiceAsync(
            followUp.AppointmentId,
            openFollow.Services[1].AppointmentServiceId,
            openFollow.Services[1].RowVersion);
        await using var afterCancelDb = scenario.CreateContext(desk);
        var blocked = await Assert.ThrowsAsync<SchedulingException>(() =>
            new AppointmentWorkflowService(afterCancelDb, desk).ProgressOwnServiceAsync(
                followUp.AppointmentId,
                followLines[1].AppointmentServiceId,
                new ProgressOwnServiceRequest { Action = "Start" }));
        Assert.Contains("closed appointment", blocked.Message, StringComparison.OrdinalIgnoreCase);
        var cancelled = await scenario.Appointments.CancelAppointmentAsync(
            followUp.AppointmentId,
            new CancelAppointmentRequest { Reason = "Customer left" });
        Assert.Equal(nameof(AppointmentStatus.Cancelled), cancelled.Status);
        Assert.Equal(nameof(ServiceLineStatus.Completed), cancelled.Services[0].LineStatus);
        Assert.False(cancelled.Services[0].IsCancelled);
        Assert.Equal(nameof(ServiceLineStatus.Cancelled), cancelled.Services[1].LineStatus);
    }

    [Fact]
    public async Task Simultaneous_allocations_cannot_book_the_same_slot_twice()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        await PrepareCustomerBookingAsync(scenario);
        var owner = CustomerWorkflow(scenario, scenario.CustomerId);
        var day = UpcomingDate();
        var first = await owner.Service.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = day,
            PreferredStartTime = On(day, 15)
        });
        var second = await owner.Service.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = day,
            PreferredStartTime = On(day, 15)
        });
        var advice = await scenario.Appointments.RecommendAllocationAsync(first.AppointmentId);
        var other = await scenario.Appointments.RecommendAllocationAsync(second.AppointmentId);
        var staff = new SchedulingUser(scenario.TenantId, scenario.BranchId);
        await using var firstDb = scenario.CreateSeparateConnection(staff);
        await using var secondDb = scenario.CreateSeparateConnection(staff);
        var left = new AppointmentWorkflowService(firstDb, staff);
        var right = new AppointmentWorkflowService(secondDb, staff);

        var outcomes = await Task.WhenAll(
            AttemptAllocation(left, first.AppointmentId, advice.Options[0]),
            AttemptAllocation(right, second.AppointmentId, other.Options[0]));

        Assert.Equal(1, outcomes.Count(item => item == null));
        Assert.Contains(outcomes, item => item != null && item.Contains("no longer available", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Simultaneous_approvals_resolve_one_cancellation_request()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        await PrepareCustomerBookingAsync(scenario);
        var owner = CustomerWorkflow(scenario, scenario.CustomerId);
        var day = UpcomingDate();
        var created = await owner.Service.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = day,
            PreferredStartTime = On(day, 16)
        });
        await owner.Service.RequestOwnCancellationAsync(created.AppointmentId);
        var staff = new SchedulingUser(scenario.TenantId, scenario.BranchId);
        await using var firstDb = scenario.CreateSeparateConnection(staff);
        await using var secondDb = scenario.CreateSeparateConnection(staff);
        var first = new AppointmentWorkflowService(firstDb, staff);
        var second = new AppointmentWorkflowService(secondDb, staff);

        var outcomes = await Task.WhenAll(
            AttemptDecision(first, created.AppointmentId),
            AttemptDecision(second, created.AppointmentId));

        Assert.Equal(1, outcomes.Count(item => item == null));
        Assert.Contains(outcomes, item => item != null && item.Contains("cancel", StringComparison.OrdinalIgnoreCase));
        var listed = Assert.Single(await owner.Service.ListOwnAppointmentsAsync());
        Assert.Equal(nameof(AppointmentStatus.Cancelled), listed.Status);
        Assert.Equal(CustomerCancellationRequest.Approved, listed.CancellationRequest);
    }

    [Fact]
    public void Salon_clock_uses_Asia_Colombo_and_hides_past_slots_only_today()
    {
        var utc = new DateTime(2026, 11, 20, 20, 0, 0, DateTimeKind.Utc);
        var now = SalonTime.Now(utc);
        Assert.Equal(new DateTime(2026, 11, 21, 1, 30, 0), now);
        Assert.Equal(DateTimeKind.Unspecified, now.Kind);
        Assert.Equal(new DateOnly(2026, 11, 21), SalonTime.Today(utc));
        Assert.True(SalonTime.IsPastSlot(new DateOnly(2026, 11, 21), new DateTime(2026, 11, 21, 1, 0, 0), utc));
        Assert.False(SalonTime.IsPastSlot(new DateOnly(2026, 11, 21), new DateTime(2026, 11, 21, 2, 0, 0), utc));
        Assert.False(SalonTime.IsPastSlot(new DateOnly(2026, 11, 9), new DateTime(2026, 11, 9, 9, 0, 0), utc));
    }

    [Fact]
    public async Task Customer_history_lists_every_service_name()
    {
        await using var scenario = await SchedulingScenario.CreateAsync();
        await PrepareCustomerBookingAsync(scenario);
        var colour = new Service
        {
            TenantId = scenario.TenantId,
            BranchId = scenario.BranchId,
            ServiceCategoryId = scenario.Db.Services.Single(item => item.ServiceId == scenario.ServiceId).ServiceCategoryId,
            ServiceCode = "SVC2",
            ServiceName = "Colour",
            DurationMinutes = 30,
            Price = 40,
            IsCustomerVisible = true
        };
        scenario.Db.Services.Add(colour);
        await scenario.Db.SaveChangesAsync();
        var owner = CustomerWorkflow(scenario, scenario.CustomerId);
        var created = await owner.Service.CreateOwnAppointmentAsync(new CreateOwnAppointmentRequest
        {
            ServiceId = scenario.ServiceId,
            AppointmentDate = AppointmentDate,
            PreferredStartTime = At(15)
        });
        await scenario.Appointments.AddServiceAsync(
            created.AppointmentId,
            new AddAppointmentServiceRequest { ServiceId = colour.ServiceId });

        owner.Db.ChangeTracker.Clear();
        var row = Assert.Single(await owner.Service.ListOwnAppointmentsAsync());
        Assert.Equal(new[] { "Cut", "Colour" }, row.ServiceNames);
        Assert.Equal("Cut, Colour", row.ServiceName);
        Assert.DoesNotContain("40", row.ServiceName, StringComparison.Ordinal);
    }

    private static ApplyAllocationRequest ToApply(AllocationOptionResponse option) => new()
    {
        Lines = option.Lines.Select(line => new ApplyAllocationLineRequest
        {
            AppointmentServiceId = line.AppointmentServiceId,
            EmployeeId = line.EmployeeId,
            StartTime = line.StartTime,
            EndTime = line.EndTime,
            RowVersion = line.RowVersion
        }).ToList()
    };

    private static async Task<string?> AttemptAllocation(
        AppointmentWorkflowService service,
        long appointmentId,
        AllocationOptionResponse option)
    {
        try
        {
            await service.ApplyAllocationAsync(appointmentId, ToApply(option));
            return null;
        }
        catch (SchedulingException exception)
        {
            return exception.Message;
        }
    }

    private static async Task<string?> AttemptDecision(AppointmentWorkflowService service, long appointmentId)
    {
        try
        {
            await service.ApproveCancellationRequestAsync(appointmentId);
            return null;
        }
        catch (SchedulingException exception)
        {
            return exception.Message;
        }
    }

    private static async Task<string?> Attempt(AppointmentWorkflowService service, long appointmentId)
    {
        try
        {
            await service.RequestOwnCancellationAsync(appointmentId);
            return null;
        }
        catch (SchedulingException exception)
        {
            return exception.Message;
        }
    }

    private static async Task PrepareCustomerBookingAsync(SchedulingScenario scenario)
    {
        var branch = await scenario.Db.Branches.SingleAsync(item => item.BranchId == scenario.BranchId);
        var service = await scenario.Db.Services.SingleAsync(item => item.ServiceId == scenario.ServiceId);
        var employee = await scenario.Db.Employees.SingleAsync(item => item.EmployeeId == scenario.EmployeeId);
        branch.IsCustomerVisible = true;
        service.IsCustomerVisible = true;
        employee.IsCustomerVisible = true;
        await scenario.Db.SaveChangesAsync();
        await scenario.Capabilities.AssignAsync(scenario.NewCapability());
        for (byte day = 0; day < 7; day++)
        {
            var hours = scenario.Working(new TimeOnly(9, 0), new TimeOnly(18, 0));
            hours.DayOfWeek = day;
            await scenario.Schedules.CreateAsync(hours);
        }
    }

    private static DateOnly UpcomingDate()
    {
        var day = SalonTime.Today().AddDays(1);
        return day;
    }

    private static DateTime On(DateOnly date, int hour, int minute = 0) =>
        new(date.Year, date.Month, date.Day, hour, minute, 0);

    private static (AppointmentWorkflowService Service, SalonBookingDbContext Db) CustomerWorkflow(
        SchedulingScenario scenario,
        long customerId)
    {
        var user = new SchedulingUser(scenario.TenantId, scenario.BranchId, UserCategory.Customer, customerId);
        var db = scenario.CreateContext(user);
        return (new AppointmentWorkflowService(db, user), db);
    }

    private static DateTime At(int hour, int minute = 0) =>
        new(AppointmentDate.Year, AppointmentDate.Month, AppointmentDate.Day, hour, minute, 0);

    private sealed class SchedulingScenario : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly SalonBookingDbContext _db;
        private readonly SchedulingUser _user;

        private SchedulingScenario(SqliteConnection connection, SalonBookingDbContext db, SchedulingUser user)
        {
            _connection = connection;
            _db = db;
            _user = user;
        }

        public SalonBookingDbContext Db => _db;
        public AppointmentWorkflowService Appointments { get; private set; } = null!;
        public EmployeeCapabilityService Capabilities { get; private set; } = null!;
        public StaffScheduleService Schedules { get; private set; } = null!;
        public StaffLeaveService Leave { get; private set; } = null!;
        public AvailabilityQueryService Availability { get; private set; } = null!;
        public long TenantId { get; private set; }
        public long OtherTenantId { get; private set; }
        public long BranchId { get; private set; }
        public long OtherBranchId { get; private set; }
        public long CustomerId { get; private set; }
        public long EmployeeId { get; private set; }
        public long ServiceId { get; private set; }

        public static async Task<SchedulingScenario> CreateAsync()
        {
            var connection = new SqliteConnection($"Data Source=file:silk-{Guid.NewGuid():N}?mode=memory&cache=shared");
            connection.DefaultTimeout = 5;
            await connection.OpenAsync();
            var user = new SchedulingUser(1, null);
            var db = new SalonBookingDbContext(Options(connection), user);
            db.Database.EnsureCreated();
            var scenario = new SchedulingScenario(connection, db, user)
            {
                Appointments = new AppointmentWorkflowService(db, user),
                Capabilities = new EmployeeCapabilityService(db, user),
                Schedules = new StaffScheduleService(db, user),
                Leave = new StaffLeaveService(db, user),
                Availability = new AvailabilityQueryService(db, user)
            };
            await scenario.SeedAsync();
            return scenario;
        }

        public CreateAppointmentRequest NewAppointment() => new()
        {
            BranchId = BranchId,
            CustomerId = CustomerId,
            AppointmentDate = AppointmentDate
        };

        public AssignEmployeeCapabilityRequest NewCapability() => new()
        {
            BranchId = BranchId,
            EmployeeId = EmployeeId,
            ServiceId = ServiceId,
            PreferenceOrder = 1
        };

        public SaveStaffScheduleRequest Working(TimeOnly start, TimeOnly end) => new()
        {
            BranchId = BranchId,
            EmployeeId = EmployeeId,
            DayOfWeek = (byte)AppointmentDate.DayOfWeek,
            SegmentType = nameof(ScheduleSegmentType.Working),
            StartTime = start,
            EndTime = end,
            IsActive = true
        };

        public SaveStaffScheduleRequest Break(TimeOnly start, TimeOnly end)
        {
            var request = Working(start, end);
            request.SegmentType = nameof(ScheduleSegmentType.Break);
            return request;
        }

        public SaveStaffLeaveRequest LeaveRequest(DateTime start, DateTime end, LeaveStatus status) => new()
        {
            BranchId = BranchId,
            EmployeeId = EmployeeId,
            StartDateTime = start,
            EndDateTime = end,
            LeaveType = nameof(LeaveType.Annual),
            Status = status.ToString(),
            Reason = "Away"
        };

        public AvailabilityQueryRequest AvailabilityQuery() => new()
        {
            BranchId = BranchId,
            ServiceId = ServiceId,
            Date = AppointmentDate,
            EmployeeId = EmployeeId
        };

        public ScheduleAppointmentServiceRequest Schedule(string rowVersion, DateTime start, DateTime end, string? reason = null) =>
            new()
            {
                EmployeeId = EmployeeId,
                StartTime = start,
                EndTime = end,
                RowVersion = rowVersion,
                Reason = reason
            };

        public async Task<AppointmentServiceResponse> ScheduleLineAsync(DateTime start, DateTime end)
        {
            var appointment = await Appointments.CreateAppointmentAsync(NewAppointment());
            var line = await Appointments.AddServiceAsync(
                appointment.AppointmentId,
                new AddAppointmentServiceRequest { ServiceId = ServiceId });
            return await Appointments.ScheduleServiceAsync(
                appointment.AppointmentId,
                line.AppointmentServiceId,
                Schedule(line.RowVersion, start, end));
        }

        public async Task<SchedulingUser> GrantUserAsync(long? employeeId, long? branchId, params string[] codes)
        {
            var role = new Role
            {
                TenantId = TenantId,
                RoleCode = "R" + Guid.NewGuid().ToString("N")[..8],
                RoleName = "Granted",
                IsActive = true
            };
            var account = new User
            {
                TenantId = TenantId,
                BranchId = branchId,
                EmployeeId = employeeId,
                Username = "u" + Guid.NewGuid().ToString("N")[..12],
                PasswordHash = "hash",
                FirstName = "Granted",
                LastName = "User",
                UserCategory = employeeId is > 0 ? UserCategory.Employee : UserCategory.General,
                IsActive = true
            };
            _db.Roles.Add(role);
            _db.Users.Add(account);
            await _db.SaveChangesAsync();
            foreach (var code in codes)
            {
                var permission = await _db.Permissions.SingleOrDefaultAsync(item => item.PermissionCode == code);
                if (permission == null)
                {
                    permission = new Permission
                    {
                        PermissionCode = code,
                        PermissionName = code,
                        IsActive = true
                    };
                    _db.Permissions.Add(permission);
                    await _db.SaveChangesAsync();
                }

                _db.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.RoleId,
                    PermissionId = permission.PermissionId
                });
            }

            _db.UserRoles.Add(new UserRole { UserId = account.UserId, RoleId = role.RoleId });
            await _db.SaveChangesAsync();
            return new SchedulingUser(TenantId, branchId, employeeId: employeeId) { UserId = account.UserId };
        }

        public SalonBookingDbContext CreateContext(long tenantId, long? branchId)
        {
            return new SalonBookingDbContext(Options(_connection), new SchedulingUser(tenantId, branchId));
        }

        public SalonBookingDbContext CreateContext(ICurrentUserService user)
        {
            return new SalonBookingDbContext(Options(_connection), user);
        }

        public SalonBookingDbContext CreateSeparateConnection(ICurrentUserService user)
        {
            var connection = new SqliteConnection(_connection.ConnectionString);
            connection.DefaultTimeout = 5;
            connection.Open();
            return new SalonBookingDbContext(Options(connection), user);
        }

        public AppointmentWorkflowService Workflow(SalonBookingDbContext db, long tenantId, long? branchId)
        {
            return new AppointmentWorkflowService(db, new SchedulingUser(tenantId, branchId));
        }

        public async ValueTask DisposeAsync()
        {
            await _db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private async Task SeedAsync()
        {
            var tenant = new Tenant
            {
                TenantCode = "T1",
                TenantName = "Tenant",
                BusinessName = "Tenant",
                SubscriptionStartDate = DateTime.UtcNow,
                SubscriptionEndDate = DateTime.UtcNow.AddYears(1)
            };
            var otherTenant = new Tenant
            {
                TenantCode = "T2",
                TenantName = "Other",
                BusinessName = "Other",
                SubscriptionStartDate = DateTime.UtcNow,
                SubscriptionEndDate = DateTime.UtcNow.AddYears(1)
            };
            _db.Tenants.AddRange(tenant, otherTenant);
            await _db.SaveChangesAsync();

            var branch = new Branch { TenantId = tenant.TenantId, BranchCode = "B1", BranchName = "Branch" };
            var otherBranch = new Branch { TenantId = tenant.TenantId, BranchCode = "B2", BranchName = "Other" };
            var category = new ServiceCategory
            {
                TenantId = tenant.TenantId,
                CategoryCode = "CUT",
                CategoryName = "Cut"
            };
            _db.Branches.AddRange(branch, otherBranch);
            _db.ServiceCategories.Add(category);
            await _db.SaveChangesAsync();

            var customer = new Customer
            {
                TenantId = tenant.TenantId,
                BranchId = branch.BranchId,
                CustomerCode = "CUS000001",
                FirstName = "Ada",
                LastName = "Lovelace",
                MobileNo = "0770000001"
            };
            var employee = new Employee
            {
                TenantId = tenant.TenantId,
                BranchId = branch.BranchId,
                EmployeeCode = "EMP000001",
                FirstName = "Grace",
                LastName = "Hopper",
                MobileNo = "0710000001"
            };
            var service = new Service
            {
                TenantId = tenant.TenantId,
                BranchId = branch.BranchId,
                ServiceCategoryId = category.ServiceCategoryId,
                ServiceCode = "SVC1",
                ServiceName = "Cut",
                DurationMinutes = 30,
                Price = 25
            };
            var user = new User
            {
                TenantId = tenant.TenantId,
                BranchId = branch.BranchId,
                Username = "scheduler",
                PasswordHash = "hash",
                FirstName = "Sched",
                LastName = "User"
            };
            _db.Customers.Add(customer);
            _db.Employees.Add(employee);
            _db.Services.Add(service);
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            _user.UserId = user.UserId;

            TenantId = tenant.TenantId;
            OtherTenantId = otherTenant.TenantId;
            BranchId = branch.BranchId;
            OtherBranchId = otherBranch.BranchId;
            CustomerId = customer.CustomerId;
            EmployeeId = employee.EmployeeId;
            ServiceId = service.ServiceId;
            _user.TenantId = tenant.TenantId;
            await GrantNotificationAccessAsync(user.UserId);
        }

        public async Task<AppointmentWorkflowService> OpenAsAsync(string permissionCode)
        {
            var permission = await _db.Permissions.SingleAsync(item => item.PermissionCode == permissionCode);
            var role = new Role
            {
                TenantId = TenantId,
                RoleCode = "R" + Guid.NewGuid().ToString("N")[..8],
                RoleName = permissionCode,
                IsActive = true
            };
            var account = new User
            {
                TenantId = TenantId,
                BranchId = BranchId,
                Username = "n" + Guid.NewGuid().ToString("N")[..12],
                PasswordHash = "hash",
                FirstName = "Notice",
                LastName = "Reader",
                UserCategory = UserCategory.General,
                IsActive = true
            };
            _db.Roles.Add(role);
            _db.Users.Add(account);
            await _db.SaveChangesAsync();
            _db.RolePermissions.Add(new RolePermission { RoleId = role.RoleId, PermissionId = permission.PermissionId });
            _db.UserRoles.Add(new UserRole { UserId = account.UserId, RoleId = role.RoleId });
            await _db.SaveChangesAsync();
            return new AppointmentWorkflowService(
                _db,
                new SchedulingUser(TenantId, BranchId) { UserId = account.UserId });
        }

        private async Task GrantNotificationAccessAsync(long userId)
        {
            var role = new Role
            {
                TenantId = TenantId,
                RoleCode = "RECEPTIONIST",
                RoleName = "Receptionist",
                IsActive = true
            };
            _db.Roles.Add(role);
            foreach (var code in PermissionCatalog.NotificationReadCodes)
            {
                _db.Permissions.Add(new Permission
                {
                    PermissionCode = code,
                    PermissionName = code,
                    IsActive = true
                });
            }

            await _db.SaveChangesAsync();
            var permissions = (await _db.Permissions.ToListAsync())
                .Where(item => PermissionCatalog.NotificationReadCodes.Contains(item.PermissionCode))
                .ToList();
            foreach (var permission in permissions)
            {
                _db.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.RoleId,
                    PermissionId = permission.PermissionId
                });
            }

            _db.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.RoleId });
            await _db.SaveChangesAsync();
        }

        private static DbContextOptions<SalonBookingDbContext> Options(SqliteConnection connection)
        {
            return new DbContextOptionsBuilder<SalonBookingDbContext>()
                .UseSqlite(connection)
                .Options;
        }
    }

    private sealed class SchedulingUser : ICurrentUserService
    {
        public SchedulingUser(
            long tenantId,
            long? branchId,
            UserCategory? category = null,
            long? customerId = null,
            long? employeeId = null)
        {
            TenantId = tenantId;
            BranchId = branchId;
            UserCategory = category ?? SalonBooking.Domain.Enums.UserCategory.General;
            CustomerId = customerId;
            EmployeeId = employeeId;
        }

        public bool IsAuthenticated => true;
        public long? UserId { get; set; }
        public long? TenantId { get; set; }
        public long? BranchId { get; }
        public string? Username => "scheduler";
        public string? Role => "Admin";
        public UserCategory? UserCategory { get; }
        public long? EmployeeId { get; }
        public long? CustomerId { get; }
        public bool IsPlatformAdministrator => false;
    }
}
