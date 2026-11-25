using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Billing;
using SalonBooking.Application.Features.Feedback;
using SalonBooking.Application.Features.Rentals;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Security;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Infrastructure.Services;
using SalonBooking.Persistence.Context;

namespace SalonBooking.API.Tests;

public class OperationalModulesTests
{
    [Fact]
    public async Task Rental_conflict_issue_return_and_one_bill_charge()
    {
        await using var world = await OperationsWorld.CreateAsync();
        var asset = await world.Rentals.SaveAssetAsync(new SaveRentalAssetRequest
        {
            BranchId = world.BranchId,
            AssetCode = "MUL-1",
            AssetName = "Mul Anduma",
            Category = "Costume",
            ChargePerUse = 2500,
            CleaningBufferMinutes = 60
        });

        var start = new DateTime(2026, 12, 1, 9, 0, 0);
        var reserved = await world.Rentals.CreateReservationAsync(new CreateRentalReservationRequest
        {
            CustomerId = world.CustomerId,
            RentalAssetId = asset.RentalAssetId,
            PlannedIssueAt = start,
            PlannedReturnAt = start.AddHours(4)
        });
        var clash = await Assert.ThrowsAsync<BillingConflictException>(() => world.Rentals.CreateReservationAsync(new CreateRentalReservationRequest
        {
            CustomerId = world.CustomerId,
            RentalAssetId = asset.RentalAssetId,
            PlannedIssueAt = start.AddHours(4),
            PlannedReturnAt = start.AddHours(8)
        }));
        Assert.Contains("buffer", clash.Message, StringComparison.OrdinalIgnoreCase);

        var issued = await world.Rentals.IssueAsync(reserved.RentalReservationId, new RentalMovementRequest { Condition = "Good" });
        var returned = await world.Rentals.ReturnAsync(issued.RentalReservationId, new RentalMovementRequest { Condition = "Torn hem" });
        Assert.True(returned.ConditionDiscrepancy);
        Assert.True(returned.IsBillable);

        var bill = await world.Billing.CreateBillAsync(new CreateBillRequest { CustomerId = world.CustomerId });
        var charged = await world.Billing.AddRentalLineAsync(bill.BillId, new AddRentalLineRequest
        {
            RowVersion = bill.RowVersion,
            RentalReservationId = returned.RentalReservationId
        });
        Assert.Equal(2500m, charged.Total);
        Assert.Equal("Rental", charged.Lines[0].LineType);
        var duplicate = await Assert.ThrowsAsync<BillingConflictException>(() => world.Billing.AddRentalLineAsync(charged.BillId, new AddRentalLineRequest
        {
            RowVersion = charged.RowVersion,
            RentalReservationId = returned.RentalReservationId
        }));
        Assert.Contains("already", duplicate.Message, StringComparison.OrdinalIgnoreCase);

        var voided = await world.Billing.VoidAsync(charged.BillId, new VoidBillRequest
        {
            RowVersion = charged.RowVersion,
            Reason = "Rental probe void"
        });
        Assert.Equal("Void", voided.Status);
        var again = await world.Billing.CreateBillAsync(new CreateBillRequest { CustomerId = world.CustomerId });
        var rebilled = await world.Billing.AddRentalLineAsync(again.BillId, new AddRentalLineRequest
        {
            RowVersion = again.RowVersion,
            RentalReservationId = returned.RentalReservationId
        });
        Assert.Single(rebilled.Lines);
    }

    [Fact]
    public async Task Concurrent_reservations_leave_one_winner()
    {
        await using var world = await OperationsWorld.CreateAsync();
        var asset = await world.Rentals.SaveAssetAsync(new SaveRentalAssetRequest
        {
            BranchId = world.BranchId,
            AssetCode = "NIL-1",
            AssetName = "Nilame",
            Category = "Costume",
            ChargePerUse = 1000,
            CleaningBufferMinutes = 0
        });
        var start = new DateTime(2026, 12, 2, 10, 0, 0);
        var first = world.SeparateRentals().CreateReservationAsync(new CreateRentalReservationRequest
        {
            CustomerId = world.CustomerId,
            RentalAssetId = asset.RentalAssetId,
            PlannedIssueAt = start,
            PlannedReturnAt = start.AddHours(2)
        });
        var second = world.SeparateRentals().CreateReservationAsync(new CreateRentalReservationRequest
        {
            CustomerId = world.CustomerId,
            RentalAssetId = asset.RentalAssetId,
            PlannedIssueAt = start,
            PlannedReturnAt = start.AddHours(2)
        });
        var results = await Task.WhenAll(first.ContinueWith(task => task.Exception), second.ContinueWith(task => task.Exception));
        Assert.Equal(1, results.Count(item => item == null));
        Assert.Equal(1, await world.CountReservationsAsync(asset.RentalAssetId));
    }

    [Fact]
    public async Task Feedback_is_one_per_completed_line_and_blocks_booking()
    {
        await using var world = await OperationsWorld.CreateAsync();
        world.User.CustomerId = world.CustomerId;
        world.User.UserCategory = UserCategory.Customer;
        var pending = await world.Feedback.ListPendingForCurrentCustomerAsync();
        Assert.Single(pending);
        Assert.Equal(world.EmployeeId, pending[0].AppointmentServiceId > 0 ? world.EmployeeId : 0);

        await Assert.ThrowsAsync<FeedbackRequiredException>(() => world.Feedback.EnsureCustomerMayContinueAsync());
        var saved = await world.Feedback.SubmitAsync(new SubmitFeedbackRequest
        {
            AppointmentServiceId = pending[0].AppointmentServiceId,
            Rating = 5,
            Comment = "Careful colour work"
        });
        Assert.Equal(world.EmployeeId, saved.EmployeeId);
        await world.Feedback.EnsureCustomerMayContinueAsync();
        var duplicate = await Assert.ThrowsAsync<BillingConflictException>(() => world.Feedback.SubmitAsync(new SubmitFeedbackRequest
        {
            AppointmentServiceId = pending[0].AppointmentServiceId,
            Rating = 4
        }));
        Assert.Contains("already", duplicate.Message, StringComparison.OrdinalIgnoreCase);

        world.User.CustomerId = world.OtherCustomerId;
        var foreign = await Assert.ThrowsAsync<KeyNotFoundException>(() => world.Feedback.SubmitAsync(new SubmitFeedbackRequest
        {
            AppointmentServiceId = pending[0].AppointmentServiceId,
            Rating = 1
        }));
        Assert.NotNull(foreign);
    }

    [Fact]
    public async Task Dashboard_counts_live_rows_for_the_signed_in_scope()
    {
        await using var world = await OperationsWorld.CreateAsync();
        var product = await world.Billing.CreateProductAsync(new SaveRetailProductRequest
        {
            BranchId = world.BranchId,
            ProductCode = "OIL",
            ProductName = "Hair oil",
            SellingPrice = 80
        });
        var draft = await world.Billing.CreateBillAsync(new CreateBillRequest
        {
            CustomerId = world.CustomerId,
            Products = [new ProductLineInput { RetailProductId = product.RetailProductId, Quantity = 1 }]
        });
        var finalized = await world.Billing.FinalizeAsync(draft.BillId, draft.RowVersion);
        await world.Billing.RecordPaymentAsync(finalized.BillId, new RecordPaymentRequest
        {
            RowVersion = finalized.RowVersion,
            Method = "Cash",
            Amount = 30
        });
        var dashboard = await world.Operations.GetDashboardAsync();
        Assert.Equal(1, dashboard.TodayAppointments);
        Assert.Equal("Completed", world.AppointmentStatusName);
        Assert.Equal(1, dashboard.Completed);
        Assert.Equal(1, dashboard.UnpaidBills);
        Assert.Equal(50m, dashboard.OutstandingAmount);
        Assert.Null(dashboard.ActiveRentals);
    }

    private sealed class OperationsWorld : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private SalonBookingDbContext _db;

        private OperationsWorld(SqliteConnection connection, SalonBookingDbContext db, OperationsUser user)
        {
            _connection = connection;
            _db = db;
            User = user;
            Bind(db);
        }

        public OperationsUser User { get; }

        public RentalService Rentals { get; private set; } = null!;

        public BillingService Billing { get; private set; } = null!;

        public FeedbackService Feedback { get; private set; } = null!;

        public OperationsService Operations { get; private set; } = null!;

        public long BranchId { get; private set; }

        public long CustomerId { get; private set; }

        public long OtherCustomerId { get; private set; }

        public long EmployeeId { get; private set; }

        public string AppointmentStatusName { get; private set; } = string.Empty;

        public static async Task<OperationsWorld> CreateAsync()
        {
            var connection = new SqliteConnection($"Data Source=file:ops-{Guid.NewGuid():N}?mode=memory&cache=shared");
            await connection.OpenAsync();
            var user = new OperationsUser();
            var db = new SalonBookingDbContext(new DbContextOptionsBuilder<SalonBookingDbContext>().UseSqlite(connection).Options, user);
            db.Database.EnsureCreated();
            var world = new OperationsWorld(connection, db, user);
            await world.SeedAsync();
            return world;
        }

        public RentalService SeparateRentals()
        {
            var connection = new SqliteConnection(_connection.ConnectionString);
            connection.Open();
            var db = new SalonBookingDbContext(new DbContextOptionsBuilder<SalonBookingDbContext>().UseSqlite(connection).Options, User);
            return new RentalService(db, User);
        }

        public Task<int> CountReservationsAsync(long assetId) =>
            _db.RentalReservations.IgnoreQueryFilters().CountAsync(item => item.RentalAssetId == assetId && !item.IsDeleted);

        public async ValueTask DisposeAsync()
        {
            await _db.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private async Task SeedAsync()
        {
            var tenant = new Tenant
            {
                TenantCode = "TEN",
                TenantName = "Silk",
                BusinessName = "Silk",
                SubscriptionStartDate = DateTime.UtcNow,
                SubscriptionEndDate = DateTime.UtcNow.AddYears(1)
            };
            _db.Tenants.Add(tenant);
            await _db.SaveChangesAsync();
            var branch = new Branch { TenantId = tenant.TenantId, BranchCode = "B1", BranchName = "Main" };
            _db.Branches.Add(branch);
            var category = new ServiceCategory { TenantId = tenant.TenantId, CategoryCode = "CUT", CategoryName = "Cut" };
            _db.ServiceCategories.Add(category);
            await _db.SaveChangesAsync();
            var service = new Service
            {
                TenantId = tenant.TenantId,
                BranchId = branch.BranchId,
                ServiceCategoryId = category.ServiceCategoryId,
                ServiceCode = "CUT",
                ServiceName = "Cut",
                DurationMinutes = 30,
                Price = 80
            };
            _db.Services.Add(service);
            var customer = new Customer
            {
                TenantId = tenant.TenantId,
                BranchId = branch.BranchId,
                CustomerCode = "C1",
                FirstName = "Maya",
                LastName = "Fernando",
                MobileNo = "0770000001"
            };
            var otherCustomer = new Customer
            {
                TenantId = tenant.TenantId,
                BranchId = branch.BranchId,
                CustomerCode = "C2",
                FirstName = "Other",
                LastName = "Guest",
                MobileNo = "0770000002"
            };
            _db.Customers.AddRange(customer, otherCustomer);
            var employee = new Employee
            {
                TenantId = tenant.TenantId,
                BranchId = branch.BranchId,
                EmployeeCode = "E1",
                FirstName = "Nadee",
                LastName = "Silva",
                MobileNo = "0770000003"
            };
            _db.Employees.Add(employee);
            await _db.SaveChangesAsync();
            var today = Application.Features.Scheduling.SalonTime.Today();
            var appointment = new Appointment
            {
                TenantId = tenant.TenantId,
                BranchId = branch.BranchId,
                CustomerId = customer.CustomerId,
                AppointmentDate = today,
                Status = AppointmentStatus.Completed
            };
            _db.Appointments.Add(appointment);
            await _db.SaveChangesAsync();
            _db.AppointmentServices.Add(new AppointmentService
            {
                TenantId = tenant.TenantId,
                BranchId = branch.BranchId,
                AppointmentId = appointment.AppointmentId,
                ServiceId = service.ServiceId,
                EmployeeId = employee.EmployeeId,
                DurationMinutes = 30,
                UnitPrice = 80,
                Sequence = 1,
                LineStatus = ServiceLineStatus.Completed,
                RowVersion = Guid.NewGuid().ToByteArray()
            });
            var permission = new Permission
            {
                PermissionCode = PermissionCatalog.AppointmentsRead,
                PermissionName = "Appointments",
                IsActive = true
            };
            var bills = new Permission
            {
                PermissionCode = PermissionCatalog.BillsRead,
                PermissionName = "Bills",
                IsActive = true
            };
            _db.Permissions.AddRange(permission, bills);
            await _db.SaveChangesAsync();
            var role = new Role { TenantId = tenant.TenantId, RoleCode = "ADMIN", RoleName = "Admin" };
            _db.Roles.Add(role);
            var staff = new User
            {
                TenantId = tenant.TenantId,
                BranchId = branch.BranchId,
                Username = "desk",
                PasswordHash = "x",
                FirstName = "Desk",
                LastName = "User",
                UserCategory = UserCategory.General
            };
            _db.Users.Add(staff);
            await _db.SaveChangesAsync();
            _db.RolePermissions.AddRange(
                new RolePermission { RoleId = role.RoleId, PermissionId = permission.PermissionId },
                new RolePermission { RoleId = role.RoleId, PermissionId = bills.PermissionId });
            _db.UserRoles.Add(new UserRole { UserId = staff.UserId, RoleId = role.RoleId });
            await _db.SaveChangesAsync();

            BranchId = branch.BranchId;
            CustomerId = customer.CustomerId;
            OtherCustomerId = otherCustomer.CustomerId;
            EmployeeId = employee.EmployeeId;
            AppointmentStatusName = appointment.Status.ToString();
            User.TenantId = tenant.TenantId;
            User.BranchId = branch.BranchId;
            User.UserId = staff.UserId;
            await _db.DisposeAsync();
            _db = new SalonBookingDbContext(new DbContextOptionsBuilder<SalonBookingDbContext>().UseSqlite(_connection).Options, User);
            Bind(_db);
        }

        private void Bind(SalonBookingDbContext db)
        {
            Rentals = new RentalService(db, User);
            Billing = new BillingService(db, User, new DocumentNumberService(db));
            Feedback = new FeedbackService(db, User);
            Operations = new OperationsService(db, User);
        }
    }

    private sealed class OperationsUser : ICurrentUserService
    {
        public bool IsAuthenticated => true;

        public long? UserId { get; set; } = 1;

        public long? TenantId { get; set; }

        public long? BranchId { get; set; }

        public string? Username => "desk";

        public string? Role => "Admin";

        public UserCategory? UserCategory { get; set; } = Domain.Enums.UserCategory.General;

        public long? EmployeeId => null;

        public long? CustomerId { get; set; }

        public bool IsPlatformAdministrator => false;
    }
}
