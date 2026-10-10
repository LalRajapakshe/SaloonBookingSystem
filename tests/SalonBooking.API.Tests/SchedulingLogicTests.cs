using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Scheduling;
using SalonBooking.Application.Interfaces;
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
            var connection = new SqliteConnection("Data Source=:memory:");
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

        public SalonBookingDbContext CreateContext(long tenantId, long? branchId)
        {
            return new SalonBookingDbContext(Options(_connection), new SchedulingUser(tenantId, branchId));
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
        public SchedulingUser(long tenantId, long? branchId)
        {
            TenantId = tenantId;
            BranchId = branchId;
        }

        public bool IsAuthenticated => true;
        public long? UserId { get; set; }
        public long? TenantId { get; set; }
        public long? BranchId { get; }
        public string? Username => "scheduler";
        public string? Role => "Admin";
    }
}
