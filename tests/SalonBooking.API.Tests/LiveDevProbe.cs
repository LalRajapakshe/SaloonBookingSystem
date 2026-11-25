using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SalonBooking.Application.Common.Settings;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Infrastructure.Authentication;
using SalonBooking.Persistence.Context;

namespace SalonBooking.API.Tests;

/// <summary>
/// Hits the development API on port 5041. It runs only when SILK_LIVE=1.
/// </summary>
public class LiveDevProbe
{
    [Fact]
    public async Task Development_api_covers_rentals_feedback_and_dashboard()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("SILK_LIVE"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var apiDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "SalonBooking.API"));
        var config = new ConfigurationBuilder()
            .SetBasePath(apiDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets("b2964430-2b9a-497e-9845-590fae01a3e8")
            .Build();
        var connectionString = config.GetConnectionString("DefaultConnection");
        Assert.False(string.IsNullOrWhiteSpace(connectionString));
        var jwt = config.GetSection("JwtSettings").Get<JwtSettings>();
        Assert.NotNull(jwt);
        Assert.False(string.IsNullOrWhiteSpace(jwt!.SecretKey));
        var tokens = new JwtTokenService(Options.Create(jwt));
        await using var db = new SalonBookingDbContext(
            new DbContextOptionsBuilder<SalonBookingDbContext>().UseSqlServer(connectionString).Options,
            new ProbeUser());
        var server = await db.Database.SqlQueryRaw<string>("SELECT @@SERVERNAME AS Value").SingleAsync();
        var database = await db.Database.SqlQueryRaw<string>("SELECT DB_NAME() AS Value").SingleAsync();
        Assert.Equal("NAGSERVER", server);
        Assert.Equal("saloonDB", database);

        using var http = new HttpClient { BaseAddress = new Uri("http://localhost:5041") };
        var admin = await TokenAsync(db, tokens, 1);
        var desk = await TokenAsync(db, tokens, 5);
        var stylist = await TokenAsync(db, tokens, 6);
        var branch10 = await TokenAsync(db, tokens, 8);
        var tenant2 = await TokenAsync(db, tokens, 9);
        var failures = new List<string>();

        async Task Check(string name, Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception exception)
            {
                failures.Add(name + ": " + exception.Message);
            }
        }

        var probe = await Send(http, HttpMethod.Get, "/api/Rentals/assets", admin, null);
        if (probe.Status == 401)
        {
            throw new InvalidOperationException("admin token rejected " + probe.Body);
        }

        await Check("unauthenticated rentals", async () =>
        {
            var response = await http.GetAsync("/api/Rentals/assets");
            if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException("status " + (int)response.StatusCode);
            }
        });
        await Check("stylist rentals forbidden", async () =>
        {
            var response = await Send(http, HttpMethod.Get, "/api/Rentals/assets", stylist, null);
            if (response.Status != 403) throw new InvalidOperationException("status " + response.Status + " " + response.Body);
        });
        await Check("desk rentals forbidden", async () =>
        {
            var response = await Send(http, HttpMethod.Get, "/api/Rentals/assets", desk, null);
            if (response.Status != 403) throw new InvalidOperationException("status " + response.Status);
        });

        JsonNode? asset = null;
        await Check("create asset", async () =>
        {
            var response = await Send(http, HttpMethod.Post, "/api/Rentals/assets", admin, new
            {
                branchId = 1,
                assetCode = "SILK-INT-" + DateTime.UtcNow.ToString("HHmmss"),
                assetName = "SILK integration costume",
                category = "Costume",
                chargePerUse = 1500,
                cleaningBufferMinutes = 60,
                isActive = true
            });
            if (response.Status != 200) throw new InvalidOperationException(response.Body);
            asset = JsonNode.Parse(response.Body);
        });
        var assetId = asset?["rentalAssetId"]?.GetValue<long>() ?? 0;

        await Check("inactive create rejected", async () =>
        {
            var response = await Send(http, HttpMethod.Post, "/api/Rentals/assets", admin, new
            {
                branchId = 1,
                assetCode = "SILK-INT-OFF",
                assetName = "Inactive",
                category = "Costume",
                chargePerUse = 1,
                cleaningBufferMinutes = 0,
                isActive = false
            });
            if (response.Status != 400) throw new InvalidOperationException("status " + response.Status);
        });

        long customerId = 0;
        await Check("probe customer", async () =>
        {
            var response = await Send(http, HttpMethod.Post, "/api/Customers", admin, new
            {
                firstName = "Silk",
                lastName = "IntProbe",
                mobileNo = "0799" + DateTime.UtcNow.ToString("HHmmss"),
                branchId = 1
            });
            if (response.Status != 200) throw new InvalidOperationException(response.Body);
            customerId = JsonNode.Parse(response.Body)!["customerId"]!.GetValue<long>();
        });

        long reservationId = 0;
        var issueAt = new DateTime(2027, 9, 1, 9, 0, 0);
        await Check("reserve and buffer conflict", async () =>
        {
            var created = await Send(http, HttpMethod.Post, "/api/Rentals/reservations", admin, new
            {
                customerId,
                rentalAssetId = assetId,
                plannedIssueAt = issueAt,
                plannedReturnAt = issueAt.AddHours(4)
            });
            if (created.Status != 200) throw new InvalidOperationException(created.Body);
            reservationId = JsonNode.Parse(created.Body)!["rentalReservationId"]!.GetValue<long>();
            var clash = await Send(http, HttpMethod.Post, "/api/Rentals/reservations", admin, new
            {
                customerId,
                rentalAssetId = assetId,
                plannedIssueAt = issueAt.AddHours(4),
                plannedReturnAt = issueAt.AddHours(8)
            });
            if (clash.Status != 409) throw new InvalidOperationException("clash " + clash.Status + " " + clash.Body);
        });

        await Check("concurrent reservation", async () =>
        {
            var start = new DateTime(2027, 9, 2, 9, 0, 0);
            var body = new
            {
                customerId,
                rentalAssetId = assetId,
                plannedIssueAt = start,
                plannedReturnAt = start.AddHours(2)
            };
            var first = Send(http, HttpMethod.Post, "/api/Rentals/reservations", admin, body);
            var second = Send(http, HttpMethod.Post, "/api/Rentals/reservations", admin, body);
            var results = await Task.WhenAll(first, second);
            var ok = results.Count(item => item.Status == 200);
            var conflict = results.Count(item => item.Status == 409);
            if (ok != 1 || conflict != 1)
            {
                throw new InvalidOperationException(string.Join(" | ", results.Select(item => item.Status + " " + item.Body)));
            }
        });

        await Check("issue return discrepancy and bill once", async () =>
        {
            var issued = await Send(http, HttpMethod.Post, $"/api/Rentals/reservations/{reservationId}/issue", admin, new { condition = "Good" });
            if (issued.Status != 200) throw new InvalidOperationException(issued.Body);
            var returned = await Send(http, HttpMethod.Post, $"/api/Rentals/reservations/{reservationId}/return", admin, new { condition = "Torn hem" });
            if (returned.Status != 200) throw new InvalidOperationException(returned.Body);
            var rental = JsonNode.Parse(returned.Body)!;
            if (rental["conditionDiscrepancy"]?.GetValue<bool>() != true) throw new InvalidOperationException("discrepancy missing");
            if (rental["chargePerUse"]?.GetValue<decimal>() != 1500m) throw new InvalidOperationException("snapshot " + rental["chargePerUse"]);
            var draft = await Send(http, HttpMethod.Post, "/api/Bills", admin, new { branchId = 1, customerId, appointmentServiceIds = Array.Empty<long>(), products = Array.Empty<object>() });
            if (draft.Status != 200) throw new InvalidOperationException(draft.Body);
            var bill = JsonNode.Parse(draft.Body)!;
            var withRental = await Send(http, HttpMethod.Post, $"/api/Bills/{bill["billId"]}/rentals", admin, new { rowVersion = bill["rowVersion"]!.GetValue<string>(), rentalReservationId = reservationId });
            if (withRental.Status != 200) throw new InvalidOperationException(withRental.Body);
            bill = JsonNode.Parse(withRental.Body)!;
            if (bill["lines"]![0]!["lineType"]!.GetValue<string>() != "Rental") throw new InvalidOperationException("line type");
            if (bill["total"]!.GetValue<decimal>() != 1500m) throw new InvalidOperationException("total " + bill["total"]);
            var duplicate = await Send(http, HttpMethod.Post, $"/api/Bills/{bill["billId"]}/rentals", admin, new { rowVersion = bill["rowVersion"]!.GetValue<string>(), rentalReservationId = reservationId });
            if (duplicate.Status != 409) throw new InvalidOperationException("duplicate " + duplicate.Status + " " + duplicate.Body);
            var finalized = await Send(http, HttpMethod.Post, $"/api/Bills/{bill["billId"]}/finalize", admin, new { rowVersion = bill["rowVersion"]!.GetValue<string>() });
            if (finalized.Status != 200) throw new InvalidOperationException(finalized.Body);
            bill = JsonNode.Parse(finalized.Body)!;
            var edited = await Send(http, HttpMethod.Post, $"/api/Bills/{bill["billId"]}/rentals", admin, new { rowVersion = bill["rowVersion"]!.GetValue<string>(), rentalReservationId = reservationId });
            if (edited.Status != 400) throw new InvalidOperationException("finalized edit " + edited.Status + " " + edited.Body);
            var voided = await Send(http, HttpMethod.Post, $"/api/Bills/{bill["billId"]}/void", admin, new { rowVersion = bill["rowVersion"]!.GetValue<string>(), reason = "SILK integration void" });
            if (voided.Status != 200) throw new InvalidOperationException(voided.Body);
            var again = await Send(http, HttpMethod.Post, "/api/Bills", admin, new { branchId = 1, customerId, appointmentServiceIds = Array.Empty<long>(), products = Array.Empty<object>() });
            var rebilled = await Send(http, HttpMethod.Post, $"/api/Bills/{JsonNode.Parse(again.Body)!["billId"]}/rentals", admin, new
            {
                rowVersion = JsonNode.Parse(again.Body)!["rowVersion"]!.GetValue<string>(),
                rentalReservationId = reservationId
            });
            if (rebilled.Status != 200) throw new InvalidOperationException(rebilled.Body);
        });

        await Check("branch user cannot read another branch rental", async () =>
        {
            var response = await Send(http, HttpMethod.Get, "/api/Rentals/assets", branch10, null);
            if (response.Status != 403) throw new InvalidOperationException("status " + response.Status);
            var tenant = await Send(http, HttpMethod.Get, "/api/Rentals/assets", tenant2, null);
            if (tenant.Status != 403) throw new InvalidOperationException("tenant status " + tenant.Status);
        });

        long lineId = 0;
        await Check("feedback eligibility and gate", async () =>
        {
            var customerUserId = await EnsureCustomerUserAsync(db, customerId);
            var customerToken = await TokenAsync(db, tokens, customerUserId);
            var appointment = await Send(http, HttpMethod.Post, "/api/Appointments", admin, new { branchId = 1, customerId, appointmentDate = "2027-08-15", notes = "SILK integration feedback" });
            if (appointment.Status != 200) throw new InvalidOperationException(appointment.Body);
            var appointmentId = JsonNode.Parse(appointment.Body)!["appointmentId"]!.GetValue<long>();
            var added = await Send(http, HttpMethod.Post, $"/api/Appointments/{appointmentId}/services", admin, new { serviceId = 1 });
            if (added.Status != 200) throw new InvalidOperationException(added.Body);
            var line = JsonNode.Parse(added.Body)!;
            lineId = line["appointmentServiceId"]!.GetValue<long>();
            var scheduled = await Send(http, HttpMethod.Post, $"/api/Appointments/{appointmentId}/services/{lineId}/schedule", admin, new
            {
                employeeId = 3,
                startTime = "2027-08-15T10:00:00",
                endTime = "2027-08-15T10:30:00",
                rowVersion = line["rowVersion"]!.GetValue<string>()
            });
            if (scheduled.Status != 200) throw new InvalidOperationException(scheduled.Body);
            var confirmed = await Send(http, HttpMethod.Post, $"/api/Appointments/{appointmentId}/status", admin, new { status = "Confirmed" });
            if (confirmed.Status != 200) throw new InvalidOperationException(confirmed.Body);
            var started = await Send(http, HttpMethod.Post, $"/api/Appointments/{appointmentId}/services/{lineId}/progress", admin, new { action = "Start" });
            if (started.Status != 200) throw new InvalidOperationException(started.Body);
            var finished = await Send(http, HttpMethod.Post, $"/api/Appointments/{appointmentId}/services/{lineId}/progress", admin, new { action = "Finish" });
            if (finished.Status != 200) throw new InvalidOperationException(finished.Body);

            var pending = await Send(http, HttpMethod.Get, "/api/CustomerFeedback/pending", customerToken, null);
            if (pending.Status != 200 || !pending.Body.Contains(lineId.ToString())) throw new InvalidOperationException(pending.Body);
            var browse = await Send(http, HttpMethod.Get, "/api/CustomerCatalogue/branches/1/services", customerToken, null);
            if (browse.Status != 409) throw new InvalidOperationException("browse " + browse.Status + " " + browse.Body);
            var book = await Send(http, HttpMethod.Post, "/api/CustomerAppointments", customerToken, new { branchId = 1, serviceId = 1, appointmentDate = "2027-08-16" });
            if (book.Status != 409) throw new InvalidOperationException("book " + book.Status);
            var mine = await Send(http, HttpMethod.Get, "/api/CustomerAppointments", customerToken, null);
            if (mine.Status != 200) throw new InvalidOperationException("history " + mine.Status);
            var account = await Send(http, HttpMethod.Get, "/api/Auth/me", customerToken, null);
            if (account.Status != 200) throw new InvalidOperationException("account " + account.Status);
            var badRating = await Send(http, HttpMethod.Post, "/api/CustomerFeedback", customerToken, new { appointmentServiceId = lineId, rating = 9 });
            if (badRating.Status != 400) throw new InvalidOperationException("rating " + badRating.Status);
            var saved = await Send(http, HttpMethod.Post, "/api/CustomerFeedback", customerToken, new { appointmentServiceId = lineId, rating = 5, comment = "SILK integration" });
            if (saved.Status != 200) throw new InvalidOperationException(saved.Body);
            var savedNode = JsonNode.Parse(saved.Body)!;
            if (savedNode["employeeId"]!.GetValue<long>() != 3) throw new InvalidOperationException("stylist " + savedNode["employeeId"]);
            var duplicate = await Send(http, HttpMethod.Post, "/api/CustomerFeedback", customerToken, new { appointmentServiceId = lineId, rating = 4 });
            if (duplicate.Status != 409) throw new InvalidOperationException("duplicate " + duplicate.Status);
            var open = await Send(http, HttpMethod.Get, "/api/CustomerCatalogue/branches/1/services", customerToken, null);
            if (open.Status != 200) throw new InvalidOperationException("reopen " + open.Status + " " + open.Body);
            var foreign = await TokenAsync(db, tokens, 2);
            var denied = await Send(http, HttpMethod.Post, "/api/CustomerFeedback", foreign, new { appointmentServiceId = lineId, rating = 1 });
            if (denied.Status != 404) throw new InvalidOperationException("foreign " + denied.Status + " " + denied.Body);
        });

        await Check("stylist sees only the assigned line", async () =>
        {
            var stylistUserId = await EnsureEmployeeUserAsync(db, "silk.int.stylist", 3);
            var otherUserId = await EnsureEmployeeUserAsync(db, "silk.int.other", 5);
            var own = await Send(http, HttpMethod.Get, "/api/Appointments/mine", await TokenAsync(db, tokens, stylistUserId), null);
            if (own.Status != 200 || !own.Body.Contains("2027-08-15")) throw new InvalidOperationException(own.Status + " " + own.Body);
            var other = await Send(http, HttpMethod.Get, "/api/Appointments/mine", await TokenAsync(db, tokens, otherUserId), null);
            if (other.Status != 200 || other.Body.Contains("2027-08-15")) throw new InvalidOperationException(other.Status + " " + other.Body);
            var ownDash = await Send(http, HttpMethod.Get, "/api/Operations/dashboard", await TokenAsync(db, tokens, stylistUserId), null);
            if (ownDash.Status != 200 || JsonNode.Parse(ownDash.Body)!["activeRentals"] != null) throw new InvalidOperationException(ownDash.Body);
            var bills = await Send(http, HttpMethod.Get, "/api/Bills", await TokenAsync(db, tokens, stylistUserId), null);
            if (bills.Status != 403) throw new InvalidOperationException("bills " + bills.Status);
        });

        await Check("dashboard is live and role scoped", async () =>
        {
            var adminDash = await Send(http, HttpMethod.Get, "/api/Operations/dashboard", admin, null);
            if (adminDash.Status != 200 || adminDash.Body.Contains("84500") || adminDash.Body.Contains("84,500")) throw new InvalidOperationException(adminDash.Body);
            var node = JsonNode.Parse(adminDash.Body)!;
            if (node["activeRentals"] == null) throw new InvalidOperationException("admin rentals hidden");
            var deskDash = await Send(http, HttpMethod.Get, "/api/Operations/dashboard", desk, null);
            if (deskDash.Status != 200) throw new InvalidOperationException(deskDash.Body);
            var deskNode = JsonNode.Parse(deskDash.Body)!;
            if (deskNode["activeRentals"] != null) throw new InvalidOperationException("desk saw rentals without the permission");
            if (deskNode["unpaidBills"] == null) throw new InvalidOperationException("desk bills missing");
            var stylistDash = await Send(http, HttpMethod.Get, "/api/Operations/dashboard", stylist, null);
            if (stylistDash.Status != 403) throw new InvalidOperationException("legacy stylist " + stylistDash.Status + " " + stylistDash.Body);
            var otherTenant = await Send(http, HttpMethod.Get, "/api/Operations/dashboard", tenant2, null);
            if (otherTenant.Status != 200) throw new InvalidOperationException(otherTenant.Body);
            if (JsonNode.Parse(otherTenant.Body)!["todayAppointments"]!.GetValue<int>() != 0)
            {
                throw new InvalidOperationException("tenant 2 saw appointments");
            }
        });

        if (failures.Count > 0)
        {
            throw new Xunit.Sdk.XunitException(string.Join(Environment.NewLine, failures));
        }
    }

    private static async Task<long> EnsureCustomerUserAsync(SalonBookingDbContext db, long customerId)
    {
        var existing = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(user => user.CustomerId == customerId && !user.IsDeleted);
        if (existing != null) return existing.UserId;
        var user = new User
        {
            TenantId = 1,
            BranchId = 1,
            Username = "silk.int.customer." + customerId,
            PasswordHash = new PasswordHasher<User>().HashPassword(new User(), Guid.NewGuid().ToString("N")),
            FirstName = "Silk",
            LastName = "IntCustomer",
            UserCategory = UserCategory.Customer,
            CustomerId = customerId
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var roleId = await db.Roles.IgnoreQueryFilters().Where(role => role.TenantId == 1 && role.RoleCode == "CUSTOMER" && !role.IsDeleted).Select(role => role.RoleId).SingleAsync();
        db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = roleId });
        await db.SaveChangesAsync();
        return user.UserId;
    }

    private static async Task<long> EnsureEmployeeUserAsync(SalonBookingDbContext db, string username, long employeeId)
    {
        var existing = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(user => user.Username == username && !user.IsDeleted);
        if (existing != null) return existing.UserId;
        var user = new User
        {
            TenantId = 1,
            BranchId = 1,
            Username = username,
            PasswordHash = new PasswordHasher<User>().HashPassword(new User(), Guid.NewGuid().ToString("N")),
            FirstName = "Silk",
            LastName = username,
            UserCategory = UserCategory.Employee,
            EmployeeId = employeeId
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var roleId = await db.Roles.IgnoreQueryFilters().Where(role => role.TenantId == 1 && role.RoleCode == "STYLIST" && !role.IsDeleted).Select(role => role.RoleId).SingleAsync();
        db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = roleId });
        await db.SaveChangesAsync();
        return user.UserId;
    }

    private static async Task<string> TokenAsync(SalonBookingDbContext db, JwtTokenService tokens, long userId)
    {
        var user = await db.Users.IgnoreQueryFilters().SingleAsync(item => item.UserId == userId);
        var roles = await (
            from link in db.UserRoles.AsNoTracking()
            join role in db.Roles.IgnoreQueryFilters().AsNoTracking() on link.RoleId equals role.RoleId
            where link.UserId == userId && !role.IsDeleted
            select role.RoleName).ToListAsync();
        return tokens.GenerateToken(user, roles);
    }

    private static async Task<(int Status, string Body)> Send(HttpClient http, HttpMethod method, string path, string token, object? body)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            var challenge = response.Headers.WwwAuthenticate.ToString();
            text = challenge + " " + text;
        }
        return ((int)response.StatusCode, text);
    }

    private sealed class ProbeUser : Application.Interfaces.ICurrentUserService
    {
        public bool IsAuthenticated => false;
        public long? UserId => null;
        public long? TenantId => 1;
        public long? BranchId => null;
        public string? Username => null;
        public string? Role => null;
        public UserCategory? UserCategory => null;
        public long? EmployeeId => null;
        public long? CustomerId => null;
        public bool IsPlatformAdministrator => false;
    }
}
