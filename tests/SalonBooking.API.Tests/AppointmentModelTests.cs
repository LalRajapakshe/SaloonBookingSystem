using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Interfaces;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Persistence.Context;

namespace SalonBooking.API.Tests;

public class AppointmentModelTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SalonBookingDbContext _db;
    private readonly SchedulingGraph _graph;

    public AppointmentModelTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<SalonBookingDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new SalonBookingDbContext(options, new ModelTestUser());
        _db.Database.EnsureCreated();
        _graph = Seed();
    }

    [Fact]
    public async Task Scheduled_line_with_employee_and_times_is_stored()
    {
        var line = NewLine(sequence: 1, employeeId: _graph.EmployeeId, start: Hour(9), end: Hour(10));

        _db.AppointmentServices.Add(line);
        await _db.SaveChangesAsync();

        var stored = await _db.AppointmentServices.SingleAsync();
        Assert.Equal(_graph.EmployeeId, stored.EmployeeId);
        Assert.Equal(60, stored.DurationMinutes);
        Assert.Equal(25.50m, stored.UnitPrice);
        Assert.False(stored.IsCancelled);
        Assert.NotNull(stored.RowVersion);
    }

    [Fact]
    public async Task Times_without_employee_are_rejected()
    {
        _db.AppointmentServices.Add(NewLine(sequence: 1, employeeId: null, start: Hour(9), end: Hour(10)));

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task Start_without_end_is_rejected()
    {
        _db.AppointmentServices.Add(NewLine(sequence: 1, employeeId: _graph.EmployeeId, start: Hour(9), end: null));

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task End_before_start_is_rejected()
    {
        _db.AppointmentServices.Add(NewLine(sequence: 1, employeeId: _graph.EmployeeId, start: Hour(10), end: Hour(9)));

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task Employee_without_times_is_allowed()
    {
        _db.AppointmentServices.Add(NewLine(sequence: 1, employeeId: _graph.EmployeeId, start: null, end: null));

        await _db.SaveChangesAsync();

        Assert.Equal(1, await _db.AppointmentServices.CountAsync());
    }

    [Fact]
    public async Task Duplicate_employee_service_capability_is_rejected()
    {
        _db.EmployeeServices.Add(Capability(1));
        await _db.SaveChangesAsync();

        _db.EmployeeServices.Add(Capability(2));

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task Duplicate_sequence_on_one_appointment_is_rejected()
    {
        _db.AppointmentServices.Add(NewLine(sequence: 1, employeeId: null, start: null, end: null));
        await _db.SaveChangesAsync();

        _db.AppointmentServices.Add(NewLine(sequence: 1, employeeId: null, start: null, end: null));

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task User_link_is_unique_when_set_and_repeatable_when_null()
    {
        var linkedUser = new User
        {
            TenantId = 1,
            Username = "linked",
            PasswordHash = "hash",
            FirstName = "Linked",
            LastName = "User"
        };
        _db.Users.Add(linkedUser);
        await _db.SaveChangesAsync();

        var second = NewEmployee("EMP002", _graph.BranchId);
        var third = NewEmployee("EMP003", _graph.BranchId);
        third.UserId = linkedUser.UserId;
        _db.Employees.AddRange(second, third);
        await _db.SaveChangesAsync();

        _db.ChangeTracker.Clear();
        var fourth = NewEmployee("EMP004", _graph.BranchId);
        fourth.UserId = linkedUser.UserId;
        _db.Employees.Add(fourth);

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task Staff_schedule_rejects_end_before_start_and_invalid_day()
    {
        _db.StaffSchedules.Add(new StaffSchedule
        {
            TenantId = 1,
            BranchId = _graph.BranchId,
            EmployeeId = _graph.EmployeeId,
            DayOfWeek = 1,
            SegmentType = ScheduleSegmentType.Working,
            StartTime = new TimeOnly(13, 0),
            EndTime = new TimeOnly(9, 0)
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());

        _db.ChangeTracker.Clear();
        _db.StaffSchedules.Add(new StaffSchedule
        {
            TenantId = 1,
            BranchId = _graph.BranchId,
            EmployeeId = _graph.EmployeeId,
            DayOfWeek = 7,
            SegmentType = ScheduleSegmentType.Break,
            StartTime = new TimeOnly(13, 0),
            EndTime = new TimeOnly(14, 0)
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task Approved_leave_window_must_end_after_it_starts()
    {
        _db.StaffLeaves.Add(new StaffLeave
        {
            TenantId = 1,
            BranchId = _graph.BranchId,
            EmployeeId = _graph.EmployeeId,
            StartDateTime = new DateTime(2026, 10, 5, 18, 0, 0),
            EndDateTime = new DateTime(2026, 10, 5, 9, 0, 0),
            LeaveType = LeaveType.Annual,
            Status = LeaveStatus.Approved,
            Reason = "Holiday"
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private SchedulingGraph Seed()
    {
        var tenant = new Tenant
        {
            TenantCode = "T1",
            TenantName = "Tenant",
            BusinessName = "Tenant",
            SubscriptionStartDate = DateTime.UtcNow,
            SubscriptionEndDate = DateTime.UtcNow.AddYears(1)
        };
        _db.Tenants.Add(tenant);
        _db.SaveChanges();

        var branch = new Branch
        {
            TenantId = tenant.TenantId,
            BranchCode = "B1",
            BranchName = "Branch"
        };
        var user = new User
        {
            TenantId = tenant.TenantId,
            Username = "owner",
            PasswordHash = "hash",
            FirstName = "Owner",
            LastName = "User"
        };
        _db.Branches.Add(branch);
        _db.Users.Add(user);
        _db.SaveChanges();

        var category = new ServiceCategory
        {
            TenantId = tenant.TenantId,
            CategoryCode = "CUT",
            CategoryName = "Cut"
        };
        _db.ServiceCategories.Add(category);
        _db.SaveChanges();

        var customer = new Customer
        {
            TenantId = tenant.TenantId,
            BranchId = branch.BranchId,
            CustomerCode = "CUS000001",
            FirstName = "Ada",
            LastName = "Lovelace",
            MobileNo = "0770000000"
        };
        var employee = NewEmployee("EMP001", branch.BranchId);
        employee.UserId = user.UserId;
        var service = new Service
        {
            TenantId = tenant.TenantId,
            BranchId = branch.BranchId,
            ServiceCategoryId = category.ServiceCategoryId,
            ServiceCode = "SVC1",
            ServiceName = "Cut",
            DurationMinutes = 60,
            Price = 25.50m
        };
        _db.Customers.Add(customer);
        _db.Employees.Add(employee);
        _db.Services.Add(service);
        _db.SaveChanges();

        var appointment = new Appointment
        {
            TenantId = tenant.TenantId,
            BranchId = branch.BranchId,
            CustomerId = customer.CustomerId,
            AppointmentDate = new DateOnly(2026, 10, 5),
            Status = AppointmentStatus.Pending
        };
        _db.Appointments.Add(appointment);
        _db.SaveChanges();

        return new SchedulingGraph(branch.BranchId, employee.EmployeeId, service.ServiceId, appointment.AppointmentId, user.UserId);
    }

    private AppointmentService NewLine(int sequence, long? employeeId, DateTime? start, DateTime? end)
    {
        return new AppointmentService
        {
            TenantId = 1,
            BranchId = _graph.BranchId,
            AppointmentId = _graph.AppointmentId,
            ServiceId = _graph.ServiceId,
            EmployeeId = employeeId,
            StartTime = start,
            EndTime = end,
            DurationMinutes = 60,
            UnitPrice = 25.50m,
            Sequence = sequence,
            RowVersion = [1]
        };
    }

    private EmployeeService Capability(int preference) => new()
    {
        TenantId = 1,
        BranchId = _graph.BranchId,
        EmployeeId = _graph.EmployeeId,
        ServiceId = _graph.ServiceId,
        PreferenceOrder = preference
    };

    private Employee NewEmployee(string code, long branchId) => new()
    {
        TenantId = 1,
        BranchId = branchId,
        EmployeeCode = code,
        FirstName = code,
        LastName = "Staff",
        MobileNo = code
    };

    private static DateTime Hour(int hour) => new(2026, 10, 5, hour, 0, 0);

    private sealed record SchedulingGraph(
        long BranchId,
        long EmployeeId,
        long ServiceId,
        long AppointmentId,
        long UserId);

    private sealed class ModelTestUser : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public long? UserId => 1;
        public long? TenantId => 1;
        public long? BranchId => null;
        public string? Username => "model-test";
        public string? Role => "Admin";
    }
}
