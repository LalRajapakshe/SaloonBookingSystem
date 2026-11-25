using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Features.Billing;
using SalonBooking.Application.Interfaces;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Infrastructure.Services;
using SalonBooking.Persistence.Context;

namespace SalonBooking.API.Tests;

public sealed class BillingLogicTests
{
    [Fact]
    public async Task Completed_services_and_products_bill_once_and_keep_their_money()
    {
        await using var world = await BillingWorld.CreateAsync();
        var second = await world.AddLineAsync(40m, ServiceLineStatus.Pending);
        await world.Billing.SaveTaxAsync(new SaveTaxSettingRequest
        {
            BranchId = world.BranchId,
            Name = "GST",
            RatePercent = 10
        });
        var product = await world.Billing.CreateProductAsync(new SaveRetailProductRequest
        {
            BranchId = world.BranchId,
            ProductCode = "SHAM",
            ProductName = "Shampoo",
            SellingPrice = 25
        });

        var notReady = await Assert.ThrowsAsync<SalonBooking.Application.Common.BillingException>(() =>
            world.Billing.CreateBillAsync(new CreateBillRequest
            {
                CustomerId = world.CustomerId,
                AppointmentServiceIds = [world.LineId, second]
            }));
        Assert.Contains("completed", notReady.Message, StringComparison.OrdinalIgnoreCase);

        await world.CompleteAsync(world.LineId);
        var bill = await world.Billing.CreateBillAsync(new CreateBillRequest
        {
            CustomerId = world.CustomerId,
            AppointmentId = world.AppointmentId,
            AppointmentServiceIds = [world.LineId],
            Products =
            [
                new ProductLineInput { RetailProductId = product.RetailProductId, Quantity = 2 }
            ]
        });

        Assert.Null(bill.BillNumber);
        Assert.Equal("Draft", bill.Status);
        Assert.Equal("Unpaid", bill.PaymentStatus);
        Assert.Equal(130m, bill.Subtotal);
        Assert.Equal(13m, bill.TaxAmount);
        Assert.Equal(143m, bill.Total);
        Assert.Equal(2, bill.Lines.Count);
        Assert.Equal("DraftInProgress", (await world.Billing.GetCoverageAsync(world.AppointmentId)).Coverage);

        var duplicate = await Assert.ThrowsAsync<SalonBooking.Application.Common.BillingConflictException>(() =>
            world.Billing.CreateBillAsync(new CreateBillRequest
            {
                CustomerId = world.CustomerId,
                AppointmentServiceIds = [world.LineId]
            }));
        Assert.Contains("already", duplicate.Message, StringComparison.OrdinalIgnoreCase);

        var finalized = await world.Billing.FinalizeAsync(bill.BillId, bill.RowVersion);
        Assert.Equal("BIL000001", finalized.BillNumber);
        Assert.Equal("Finalized", finalized.Status);
        Assert.Equal(143m, finalized.Outstanding);

        var part = await world.Billing.RecordPaymentAsync(finalized.BillId, new RecordPaymentRequest
        {
            Method = "Cash",
            Amount = 43,
            RowVersion = finalized.RowVersion
        });
        Assert.Equal("PartiallyPaid", part.PaymentStatus);
        Assert.Equal(100m, part.Outstanding);

        var over = await Assert.ThrowsAsync<SalonBooking.Application.Common.BillingException>(() =>
            world.Billing.RecordPaymentAsync(part.BillId, new RecordPaymentRequest
            {
                Method = "Card",
                Amount = 100.01m,
                RowVersion = part.RowVersion
            }));
        Assert.Contains("outstanding", over.Message, StringComparison.OrdinalIgnoreCase);

        var paid = await world.Billing.RecordPaymentAsync(part.BillId, new RecordPaymentRequest
        {
            Method = "Card",
            Amount = 100,
            RowVersion = part.RowVersion
        });
        Assert.Equal("Paid", paid.PaymentStatus);
        Assert.Equal(0m, paid.Outstanding);

        var tooMuch = await Assert.ThrowsAsync<SalonBooking.Application.Common.BillingException>(() =>
            world.Billing.RecordRefundAsync(paid.BillId, new RecordPaymentRequest
            {
                Method = "Cash",
                Amount = 143.01m,
                RowVersion = paid.RowVersion
            }));
        Assert.Contains("collected", tooMuch.Message, StringComparison.OrdinalIgnoreCase);

        var blocked = await Assert.ThrowsAsync<SalonBooking.Application.Common.BillingException>(() =>
            world.Billing.VoidAsync(paid.BillId, new VoidBillRequest { Reason = "Mistake", RowVersion = paid.RowVersion }));
        Assert.Contains("Refund", blocked.Message, StringComparison.OrdinalIgnoreCase);

        var refunded = await world.Billing.RecordRefundAsync(paid.BillId, new RecordPaymentRequest
        {
            Method = "Cash",
            Amount = 143,
            RowVersion = paid.RowVersion
        });
        Assert.Equal(0m, refunded.NetCollected);
        Assert.Equal(3, refunded.Payments.Count);
        Assert.Contains(refunded.Payments, item => item.Kind == "Refund" && item.Amount == -143m);

        var voided = await world.Billing.VoidAsync(refunded.BillId, new VoidBillRequest
        {
            Reason = "Customer changed their mind",
            RowVersion = refunded.RowVersion
        });
        Assert.Equal("Void", voided.Status);
        Assert.Equal("BIL000001", voided.BillNumber);
        Assert.Equal(0, await world.CountClaimsAsync(world.LineId));

        var again = await world.Billing.CreateBillAsync(new CreateBillRequest
        {
            CustomerId = world.CustomerId,
            AppointmentServiceIds = [world.LineId]
        });
        var secondBill = await world.Billing.FinalizeAsync(again.BillId, again.RowVersion);
        Assert.Equal("BIL000002", secondBill.BillNumber);
        Assert.Equal("FullyBilled", (await world.Billing.GetCoverageAsync(world.AppointmentId)).Coverage);
        Assert.Empty(await world.Billing.ListEligibleServicesAsync(world.AppointmentId));
    }

    [Fact]
    public async Task A_product_only_bill_needs_no_appointment_and_another_branch_cannot_read_it()
    {
        await using var world = await BillingWorld.CreateAsync();
        var product = await world.Billing.CreateProductAsync(new SaveRetailProductRequest
        {
            BranchId = world.BranchId,
            ProductCode = "OIL",
            ProductName = "Hair oil",
            SellingPrice = 15
        });
        var bill = await world.Billing.CreateBillAsync(new CreateBillRequest
        {
            CustomerId = world.CustomerId,
            Products = [new ProductLineInput { RetailProductId = product.RetailProductId, Quantity = 1 }]
        });
        Assert.Null(bill.AppointmentId);
        Assert.Equal(15m, bill.Total);

        world.User.BranchId = world.OtherBranchId;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => world.Billing.GetBillAsync(bill.BillId));
        world.User.TenantId = world.OtherTenantId;
        world.User.BranchId = null;
        await Assert.ThrowsAsync<KeyNotFoundException>(() => world.Billing.GetBillAsync(bill.BillId));
    }

    [Fact]
    public async Task Two_concurrent_bills_cannot_claim_the_same_service()
    {
        await using var world = await BillingWorld.CreateAsync();
        await world.CompleteAsync(world.LineId);
        var first = Task.Run(async () =>
        {
            await using var db = world.Separate();
            var billing = new BillingService(db, world.User, new DocumentNumberService(db));
            return await billing.CreateBillAsync(new CreateBillRequest
            {
                CustomerId = world.CustomerId,
                AppointmentServiceIds = [world.LineId]
            });
        });
        var second = Task.Run(async () =>
        {
            await using var db = world.Separate();
            var billing = new BillingService(db, world.User, new DocumentNumberService(db));
            return await billing.CreateBillAsync(new CreateBillRequest
            {
                CustomerId = world.CustomerId,
                AppointmentServiceIds = [world.LineId]
            });
        });

        var results = await Task.WhenAll(
            RecordAsync(first),
            RecordAsync(second));
        var detail = string.Join(" | ", results.Select(item => item == null ? "ok" : item.GetType().Name + ": " + item.Message));
        Assert.True(results.Count(item => item == null) == 1, detail);
        Assert.Equal(1, await world.CountClaimsAsync(world.LineId));
    }

    [Fact]
    public async Task A_finalized_bill_keeps_its_tax_snapshot_when_the_branch_rate_changes()
    {
        await using var world = await BillingWorld.CreateAsync();
        await world.CompleteAsync(world.LineId);
        var unset = await world.Billing.GetTaxAsync(world.BranchId);
        Assert.False(unset.Configured);
        Assert.Equal(0m, unset.RatePercent);

        var draft = await world.Billing.CreateBillAsync(new CreateBillRequest
        {
            CustomerId = world.CustomerId,
            AppointmentServiceIds = [world.LineId]
        });
        Assert.Equal(0m, draft.TaxAmount);
        Assert.Equal(80m, draft.Total);

        var finalized = await world.Billing.FinalizeAsync(draft.BillId, draft.RowVersion);
        await world.Billing.SaveTaxAsync(new SaveTaxSettingRequest
        {
            BranchId = world.BranchId,
            Name = "VAT",
            RatePercent = 12.5m
        });
        var reloaded = await world.Billing.GetBillAsync(finalized.BillId);
        Assert.Equal(0m, reloaded.TaxAmount);
        Assert.Equal(80m, reloaded.Total);
        Assert.Equal("BIL000001", reloaded.BillNumber);
        Assert.All(reloaded.Lines, line => Assert.Equal(0m, line.TaxRatePercent));
    }

    [Fact]
    public async Task Cancelled_and_inactive_items_cannot_be_sold()
    {
        await using var world = await BillingWorld.CreateAsync();
        await world.CompleteAsync(world.LineId);
        await world.CancelLineAsync(world.LineId);
        var cancelled = await Assert.ThrowsAsync<SalonBooking.Application.Common.BillingException>(() =>
            world.Billing.CreateBillAsync(new CreateBillRequest
            {
                CustomerId = world.CustomerId,
                AppointmentServiceIds = [world.LineId]
            }));
        Assert.Contains("completed", cancelled.Message, StringComparison.OrdinalIgnoreCase);

        var product = await world.Billing.CreateProductAsync(new SaveRetailProductRequest
        {
            BranchId = world.BranchId,
            ProductCode = "WAX",
            ProductName = "Styling wax",
            Description = "Matte finish",
            SellingPrice = 12
        });
        await world.Billing.UpdateProductAsync(product.RetailProductId, new SaveRetailProductRequest
        {
            ProductCode = "WAX",
            ProductName = "Styling wax",
            Description = "Matte finish",
            SellingPrice = 12,
            IsActive = false
        });
        var hidden = await world.Billing.ListProductsAsync(new RetailProductQuery { Search = "Matte" });
        Assert.Empty(hidden.Items);
        var catalogue = await world.Billing.ListProductsAsync(new RetailProductQuery
        {
            Search = "Matte",
            IncludeInactive = true
        });
        Assert.Single(catalogue.Items);
        Assert.False(catalogue.Items[0].IsActive);

        var rejected = await Assert.ThrowsAsync<SalonBooking.Application.Common.BillingException>(() =>
            world.Billing.CreateBillAsync(new CreateBillRequest
            {
                CustomerId = world.CustomerId,
                Products = [new ProductLineInput { RetailProductId = product.RetailProductId, Quantity = 1 }]
            }));
        Assert.Contains("active", rejected.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Money_rounds_half_away_from_zero_and_keeps_payment_state_separate()
    {
        Assert.Equal(10.01m, SalonBooking.Domain.Billing.Money.Round(10.005m));
        Assert.Equal(18.50m, SalonBooking.Domain.Billing.Money.TaxAmount(100m, 18.5m));
        Assert.Equal(PaymentStatus.PartiallyPaid, SalonBooking.Domain.Billing.Money.PaymentState(100m, 40m, true));
        Assert.Equal(PaymentStatus.Unpaid, SalonBooking.Domain.Billing.Money.PaymentState(100m, 0m, false));
        Assert.Equal(PaymentStatus.Paid, SalonBooking.Domain.Billing.Money.PaymentState(0m, 0m, true));
    }

    private static async Task<Exception?> RecordAsync(Task task)
    {
        try
        {
            await task;
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private sealed class BillingWorld : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private SalonBookingDbContext _db;

        private BillingWorld(SqliteConnection connection, SalonBookingDbContext db, BillingUser user)
        {
            _connection = connection;
            _db = db;
            User = user;
            Billing = new BillingService(db, user, new DocumentNumberService(db));
        }

        public BillingUser User { get; }

        public BillingService Billing { get; private set; }

        public long TenantId { get; private set; }

        public long OtherTenantId { get; private set; }

        public long BranchId { get; private set; }

        public long OtherBranchId { get; private set; }

        public long CustomerId { get; private set; }

        public long AppointmentId { get; private set; }

        public long LineId { get; private set; }

        public long ServiceId { get; private set; }

        public static async Task<BillingWorld> CreateAsync()
        {
            var connection = new SqliteConnection($"Data Source=file:bill-{Guid.NewGuid():N}?mode=memory&cache=shared");
            await connection.OpenAsync();
            var user = new BillingUser();
            var db = new SalonBookingDbContext(ContextOptions(connection), user);
            db.Database.EnsureCreated();
            var world = new BillingWorld(connection, db, user);
            await world.SeedAsync();
            return world;
        }

        public SalonBookingDbContext Separate()
        {
            var connection = new SqliteConnection(_connection.ConnectionString);
            connection.DefaultTimeout = 5;
            connection.Open();
            using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA busy_timeout = 5000;";
            pragma.ExecuteNonQuery();
            return new SalonBookingDbContext(ContextOptions(connection), User);
        }

        public async Task<long> AddLineAsync(decimal price, ServiceLineStatus status)
        {
            var line = new AppointmentService
            {
                TenantId = TenantId,
                BranchId = BranchId,
                AppointmentId = AppointmentId,
                ServiceId = ServiceId,
                DurationMinutes = 30,
                UnitPrice = price,
                Sequence = 2,
                LineStatus = status,
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            _db.AppointmentServices.Add(line);
            await _db.SaveChangesAsync();
            return line.AppointmentServiceId;
        }

        public async Task CompleteAsync(long lineId)
        {
            var appointment = await _db.Appointments.IgnoreQueryFilters().SingleAsync(item => item.AppointmentId == AppointmentId);
            var line = await _db.AppointmentServices.IgnoreQueryFilters().SingleAsync(item => item.AppointmentServiceId == lineId);
            appointment.Status = AppointmentStatus.Completed;
            line.LineStatus = ServiceLineStatus.Completed;
            line.IsCancelled = false;
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
        }

        public Task<int> CountClaimsAsync(long lineId) =>
            _db.ActiveServiceCharges.IgnoreQueryFilters().CountAsync(item => item.AppointmentServiceId == lineId);

        public async Task CancelLineAsync(long lineId)
        {
            var line = await _db.AppointmentServices.IgnoreQueryFilters().SingleAsync(item => item.AppointmentServiceId == lineId);
            line.IsCancelled = true;
            line.LineStatus = ServiceLineStatus.Cancelled;
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
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
                TenantCode = "TEN",
                TenantName = "Silk",
                BusinessName = "Silk",
                SubscriptionStartDate = DateTime.UtcNow,
                SubscriptionEndDate = DateTime.UtcNow.AddYears(1)
            };
            var other = new Tenant
            {
                TenantCode = "TEN2",
                TenantName = "Other",
                BusinessName = "Other",
                SubscriptionStartDate = DateTime.UtcNow,
                SubscriptionEndDate = DateTime.UtcNow.AddYears(1)
            };
            _db.Tenants.AddRange(tenant, other);
            await _db.SaveChangesAsync();
            var branch = new Branch { TenantId = tenant.TenantId, BranchCode = "B1", BranchName = "Main" };
            var otherBranch = new Branch { TenantId = tenant.TenantId, BranchCode = "B2", BranchName = "Other" };
            _db.Branches.AddRange(branch, otherBranch);
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
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync();
            var appointment = new Appointment
            {
                TenantId = tenant.TenantId,
                BranchId = branch.BranchId,
                CustomerId = customer.CustomerId,
                AppointmentDate = new DateOnly(2026, 11, 24),
                Status = AppointmentStatus.Confirmed
            };
            _db.Appointments.Add(appointment);
            await _db.SaveChangesAsync();
            var line = new AppointmentService
            {
                TenantId = tenant.TenantId,
                BranchId = branch.BranchId,
                AppointmentId = appointment.AppointmentId,
                ServiceId = service.ServiceId,
                DurationMinutes = 30,
                UnitPrice = 80,
                Sequence = 1,
                LineStatus = ServiceLineStatus.Completed,
                RowVersion = Guid.NewGuid().ToByteArray()
            };
            _db.AppointmentServices.Add(line);
            await _db.SaveChangesAsync();

            TenantId = tenant.TenantId;
            OtherTenantId = other.TenantId;
            BranchId = branch.BranchId;
            OtherBranchId = otherBranch.BranchId;
            CustomerId = customer.CustomerId;
            AppointmentId = appointment.AppointmentId;
            LineId = line.AppointmentServiceId;
            ServiceId = service.ServiceId;
            User.TenantId = tenant.TenantId;
            User.BranchId = branch.BranchId;
            await _db.DisposeAsync();
            _db = new SalonBookingDbContext(ContextOptions(_connection), User);
            Billing = new BillingService(_db, User, new DocumentNumberService(_db));
        }

        private static DbContextOptions<SalonBookingDbContext> ContextOptions(SqliteConnection connection) =>
            new DbContextOptionsBuilder<SalonBookingDbContext>().UseSqlite(connection).Options;
    }

    private sealed class BillingUser : ICurrentUserService
    {
        public bool IsAuthenticated => true;

        public long? UserId => 7;

        public long? TenantId { get; set; }

        public long? BranchId { get; set; }

        public string? Username => "cashier";

        public string? Role => "Receptionist";

        public SalonBooking.Domain.Enums.UserCategory? UserCategory =>
            SalonBooking.Domain.Enums.UserCategory.General;

        public long? EmployeeId => null;

        public long? CustomerId => null;

        public bool IsPlatformAdministrator => false;
    }
}
