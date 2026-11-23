using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Security;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Infrastructure.Authentication;

namespace SalonBooking.API.Tests;

[Collection(ApiCollection.Name)]
public class SilkAccessTests
{
    private readonly ApiFactory _factory;

    public SilkAccessTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Catalog_has_each_new_permission_once_and_roles_follow_the_matrix()
    {
        var stored = await _factory.ReadAsync(async db =>
        {
            var codes = await db.Permissions.IgnoreQueryFilters()
                .Where(item => !item.IsDeleted)
                .Select(item => item.PermissionCode)
                .ToListAsync();
            var tenantId = _factory.TenantAId;
            var roles = await db.Roles.IgnoreQueryFilters()
                .Where(role => role.TenantId == tenantId && !role.IsDeleted)
                .Select(role => role.RoleCode)
                .ToListAsync();
            var links = await (
                from role in db.Roles.IgnoreQueryFilters()
                join link in db.RolePermissions on role.RoleId equals link.RoleId
                join permission in db.Permissions on link.PermissionId equals permission.PermissionId
                where role.TenantId == tenantId && !role.IsDeleted && !permission.IsDeleted
                select new { role.RoleCode, permission.PermissionCode }
            ).ToListAsync();
            return new { codes, roles, links };
        });

        Assert.Equal(38, stored.codes.Count);
        Assert.Equal(38, stored.codes.Distinct(StringComparer.Ordinal).Count());
        foreach (var code in PermissionCatalog.ExplicitAssignmentCodes)
        {
            Assert.Equal(1, stored.codes.Count(item => item == code));
        }

        foreach (var code in new[] { "ADMIN", "STAFF", "CUSTOMER", "STYLIST", "RECEPTIONIST", "SUPERVISOR" })
        {
            Assert.Contains(code, stored.roles);
        }

        var byRole = stored.links
            .GroupBy(item => item.RoleCode)
            .ToDictionary(group => group.Key, group => group.Select(item => item.PermissionCode).ToHashSet(StringComparer.Ordinal));

        Assert.DoesNotContain(PermissionCatalog.PlatformManage, byRole["ADMIN"]);
        Assert.DoesNotContain(PermissionCatalog.AppointmentsOwnRead, byRole["ADMIN"]);
        Assert.DoesNotContain(PermissionCatalog.AppointmentsOwnProgress, byRole["ADMIN"]);
        Assert.DoesNotContain(PermissionCatalog.AppointmentsLinesProgress, byRole["ADMIN"]);
        Assert.True(PermissionCatalog.NotificationReadCodes.All(byRole["ADMIN"].Contains));
        Assert.True(PermissionCatalog.StylistRoleCodes.SetEquals(byRole["STYLIST"]));
        Assert.True(PermissionCatalog.ReceptionistRoleCodes.SetEquals(byRole["RECEPTIONIST"]));
        Assert.True(PermissionCatalog.SupervisorRoleCodes.SetEquals(byRole["SUPERVISOR"]));
        Assert.DoesNotContain(PermissionCatalog.AppointmentsManage, byRole["SUPERVISOR"]);
        Assert.False(byRole["STAFF"].Overlaps(PermissionCatalog.ExplicitAssignmentCodes));
        Assert.True(PermissionCatalog.CustomerCodes.SetEquals(byRole["CUSTOMER"]));
    }

    [Fact]
    public async Task Bootstrap_does_not_backfill_existing_roles_with_the_new_codes()
    {
        var tenantId = await _factory.ReadAsync(async db =>
        {
            var tenant = new Tenant
            {
                TenantCode = "TEN-SILK",
                TenantName = "Silk Roles",
                BusinessName = "Silk Roles",
                SubscriptionPlan = SubscriptionPlan.Trial,
                SubscriptionStartDate = DateTime.UtcNow,
                SubscriptionEndDate = DateTime.UtcNow.AddYears(1),
                MaxBranches = 2,
                MaxUsers = 5
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            await AccessControlBootstrap.EnsureTenantRolesAsync(db, tenant.TenantId);

            var admin = await db.Roles.IgnoreQueryFilters().SingleAsync(role =>
                role.TenantId == tenant.TenantId && role.RoleCode == "ADMIN" && !role.IsDeleted);
            var booking = await db.Permissions.SingleAsync(item => item.PermissionCode == PermissionCatalog.NotificationsBookingsRead);
            var link = await db.RolePermissions.SingleAsync(item => item.RoleId == admin.RoleId && item.PermissionId == booking.PermissionId);
            db.RolePermissions.Remove(link);
            await db.SaveChangesAsync();
            await AccessControlBootstrap.EnsureTenantRolesAsync(db, tenant.TenantId);
            return tenant.TenantId;
        });

        var restored = await _factory.ReadAsync(db =>
            db.RolePermissions.AnyAsync(item =>
                db.Roles.Any(role =>
                    role.RoleId == item.RoleId &&
                    role.TenantId == tenantId &&
                    role.RoleCode == "ADMIN") &&
                db.Permissions.Any(permission =>
                    permission.PermissionId == item.PermissionId &&
                    permission.PermissionCode == PermissionCatalog.NotificationsBookingsRead)));
        var staffHasOwn = await _factory.ReadAsync(db =>
            db.RolePermissions.AnyAsync(item =>
                db.Roles.Any(role =>
                    role.RoleId == item.RoleId &&
                    role.TenantId == tenantId &&
                    role.RoleCode == "STAFF") &&
                db.Permissions.Any(permission =>
                    permission.PermissionId == item.PermissionId &&
                    permission.PermissionCode == PermissionCatalog.AppointmentsOwnRead)));

        Assert.False(restored);
        Assert.False(staffHasOwn);
    }

    [Fact]
    public async Task Salon_admin_cannot_see_or_change_a_platform_administrator()
    {
        var hasher = new PasswordHasher<User>();
        var platformId = await _factory.ReadAsync(async db =>
        {
            var user = new User
            {
                TenantId = _factory.TenantAId,
                Username = "platform.hidden",
                FirstName = "Platform",
                LastName = "Hidden",
                UserCategory = UserCategory.General,
                IsPlatformAdministrator = true,
                IsActive = true
            };
            user.PasswordHash = hasher.HashPassword(user, ApiFactory.Password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user.UserId;
        });

        using var salon = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        var listed = await salon.GetFromJsonAsync<Page<UserItem>>("/api/Users?pageSize=100");
        Assert.DoesNotContain(listed!.Items, item => item.UserId == platformId);
        Assert.Equal(HttpStatusCode.NotFound, (await salon.GetAsync($"/api/Users/{platformId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await salon.PutAsJsonAsync($"/api/Users/{platformId}", new
        {
            username = "platform.hidden",
            firstName = "Changed",
            lastName = "Hidden",
            userCategory = "General",
            roleIds = Array.Empty<long>()
        })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await salon.PostAsJsonAsync($"/api/Users/{platformId}/activation", new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await salon.PutAsJsonAsync($"/api/Users/{platformId}/roles", new { roleIds = Array.Empty<long>() })).StatusCode);

        using var platform = await _factory.CreateAuthenticatedClientAsync("platform.hidden");
        var visible = await platform.GetFromJsonAsync<Page<UserItem>>("/api/Users?pageSize=100");
        Assert.Contains(visible!.Items, item => item.UserId == platformId);
        Assert.Contains(visible.Items, item => item.Username == ApiFactory.TenantUserAName);
        Assert.Equal(HttpStatusCode.OK, (await platform.GetAsync($"/api/Users/{_factory.TenantUserAId}")).StatusCode);

        var otherPlatformId = await _factory.ReadAsync(async db =>
        {
            var user = new User
            {
                TenantId = _factory.TenantBId,
                Username = "platform.other",
                FirstName = "Other",
                LastName = "Platform",
                UserCategory = UserCategory.General,
                IsPlatformAdministrator = true,
                IsActive = true
            };
            user.PasswordHash = hasher.HashPassword(user, ApiFactory.Password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user.UserId;
        });
        Assert.Equal(HttpStatusCode.NotFound, (await platform.GetAsync($"/api/Users/{otherPlatformId}")).StatusCode);
    }

    [Fact]
    public async Task Stylist_receptionist_supervisor_and_combined_roles_stay_in_their_bounds()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        var me = await admin.GetFromJsonAsync<MeItem>("/api/Auth/me");
        Assert.DoesNotContain(PermissionCatalog.PlatformManage, me!.Permissions);
        Assert.DoesNotContain(PermissionCatalog.AppointmentsOwnRead, me.Permissions);
        Assert.Contains(PermissionCatalog.NotificationsBookingsRead, me.Permissions);
        Assert.Contains(PermissionCatalog.UsersManage, me.Permissions);

        var stylistRole = await RoleIdAsync(admin, PermissionCatalog.StylistRoleCode);
        var receptionistRole = await RoleIdAsync(admin, PermissionCatalog.ReceptionistRoleCode);
        var supervisorRole = await RoleIdAsync(admin, PermissionCatalog.SupervisorRoleCode);
        var employeeId = await AddEmployeeAsync("EMP-SILK-STYLIST");

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/Users", UserBody(
            "silk.stylist", "Employee", stylistRole, employeeId: employeeId))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/Users", UserBody(
            "silk.desk", "General", receptionistRole, branchId: _factory.BranchA1Id))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/Users", UserBody(
            "silk.supervisor", "General", supervisorRole, branchId: _factory.BranchA1Id))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/Users", UserBody(
            "silk.combined", "General", receptionistRole, supervisorRole, branchId: _factory.BranchA1Id))).StatusCode);

        using var stylist = await _factory.CreateAuthenticatedClientAsync("silk.stylist");
        Assert.Equal(HttpStatusCode.Forbidden, (await stylist.GetAsync("/api/Appointments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stylist.GetAsync("/api/Employees")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stylist.GetAsync("/api/Users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stylist.GetAsync("/api/Appointments/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await stylist.GetAsync("/api/Appointments/mine")).StatusCode);
        var stylistMe = await stylist.GetFromJsonAsync<MeItem>("/api/Auth/me");
        Assert.Contains(PermissionCatalog.AppointmentsOwnRead, stylistMe!.Permissions);
        Assert.DoesNotContain(PermissionCatalog.AppointmentsOwnProgress, stylistMe.Permissions);
        Assert.DoesNotContain(PermissionCatalog.AppointmentsLinesProgress, stylistMe.Permissions);
        Assert.Equal(HttpStatusCode.Forbidden, (await stylist.PostAsJsonAsync(
            "/api/Appointments/1/services/1/progress",
            new { action = "Commence", actualStart = "2000-01-01T00:00:00Z" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stylist.PostAsJsonAsync("/api/Appointments/1/status", new { status = "InProgress" })).StatusCode);

        using var desk = await _factory.CreateAuthenticatedClientAsync("silk.desk");
        Assert.Equal(HttpStatusCode.OK, (await desk.GetAsync("/api/Appointments")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await desk.GetAsync("/api/Customers")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await desk.GetAsync("/api/Services")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await desk.GetAsync("/api/Branch")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await desk.GetAsync("/api/StaffSchedules")).StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, (await desk.GetAsync("/api/Availability?branchId=" + _factory.BranchA1Id + "&serviceId=" + _factory.ServiceA1Id + "&date=2026-12-01")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await desk.GetAsync("/api/Appointments/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await desk.GetAsync("/api/Employees")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await desk.GetAsync("/api/Users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await desk.GetAsync("/api/Roles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await desk.PostAsJsonAsync("/api/Branch", new { branchCode = "NO", branchName = "No" })).StatusCode);

        using var supervisor = await _factory.CreateAuthenticatedClientAsync("silk.supervisor");
        var supervisorMe = await supervisor.GetFromJsonAsync<MeItem>("/api/Auth/me");
        Assert.Contains(PermissionCatalog.AppointmentsRead, supervisorMe!.Permissions);
        Assert.Contains(PermissionCatalog.SchedulesRead, supervisorMe.Permissions);
        Assert.Contains(PermissionCatalog.AppointmentsLinesProgress, supervisorMe.Permissions);
        Assert.DoesNotContain(PermissionCatalog.AppointmentsOwnProgress, supervisorMe.Permissions);
        Assert.DoesNotContain(PermissionCatalog.AppointmentsManage, supervisorMe.Permissions);
        Assert.DoesNotContain(PermissionCatalog.NotificationsBookingsRead, supervisorMe.Permissions);
        Assert.Equal(HttpStatusCode.OK, (await supervisor.GetAsync("/api/Appointments")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await supervisor.GetAsync("/api/StaffSchedules")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await supervisor.GetAsync("/api/Employees")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await supervisor.GetAsync("/api/Appointments/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await supervisor.PostAsJsonAsync(
            "/api/Appointments/999999/services/1/progress",
            new { action = "Commence" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await desk.PostAsJsonAsync(
            "/api/Appointments/999999/services/1/progress",
            new { action = "Commence", actualStart = "2000-01-01T00:00:00Z" })).StatusCode);
        var deskMe = await desk.GetFromJsonAsync<MeItem>("/api/Auth/me");
        Assert.Contains(PermissionCatalog.AppointmentsLinesProgress, deskMe!.Permissions);
        Assert.Contains(PermissionCatalog.AppointmentsManage, deskMe.Permissions);
        Assert.DoesNotContain(PermissionCatalog.AppointmentsOwnProgress, deskMe.Permissions);
        Assert.Equal(HttpStatusCode.Forbidden, (await supervisor.PostAsJsonAsync("/api/Appointments", new
        {
            branchId = _factory.BranchA1Id,
            customerId = _factory.CustomerA1Id,
            appointmentDate = "2026-12-01"
        })).StatusCode);

        using var combined = await _factory.CreateAuthenticatedClientAsync("silk.combined");
        var combinedMe = await combined.GetFromJsonAsync<MeItem>("/api/Auth/me");
        Assert.Contains(PermissionCatalog.AppointmentsManage, combinedMe!.Permissions);
        Assert.Contains(PermissionCatalog.SchedulesRead, combinedMe.Permissions);
        Assert.Contains(PermissionCatalog.NotificationsCancellationsRead, combinedMe.Permissions);
        Assert.Contains(PermissionCatalog.AppointmentsLinesProgress, combinedMe.Permissions);
        Assert.DoesNotContain(PermissionCatalog.EmployeesRead, combinedMe.Permissions);
        Assert.DoesNotContain(PermissionCatalog.UsersRead, combinedMe.Permissions);
        Assert.Equal(HttpStatusCode.OK, (await combined.GetAsync("/api/Appointments/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await combined.GetAsync("/api/Employees")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await combined.GetAsync("/api/Users")).StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_call_staff_or_notification_apis()
    {
        using (var admin = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName))
        {
            var customerRole = await RoleIdAsync(admin, PermissionCatalog.CustomerRoleCode);
            var created = await admin.PostAsJsonAsync("/api/Users", UserBody(
                "silk.customer", "Customer", customerRole, customerId: _factory.CustomerA2Id));
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        }

        using var customer = await _factory.CreateAuthenticatedClientAsync("silk.customer");
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/Appointments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/Appointments/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/Employees")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/Users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync(
            "/api/Appointments/1/services/1/progress")).StatusCode);
    }

    private async Task<long> AddEmployeeAsync(string code)
    {
        return await _factory.ReadAsync(async db =>
        {
            var employee = new Employee
            {
                TenantId = _factory.TenantAId,
                BranchId = _factory.BranchA1Id,
                EmployeeCode = code,
                FirstName = "Stylist",
                LastName = "Test",
                MobileNo = "0710099911",
                Designation = "Stylist",
                HireDate = new DateTime(2020, 1, 1),
                Salary = 1000
            };
            db.Employees.Add(employee);
            await db.SaveChangesAsync();
            return employee.EmployeeId;
        });
    }

    private static async Task<long> RoleIdAsync(HttpClient client, string code)
    {
        var roles = await client.GetFromJsonAsync<List<RoleItem>>("/api/Roles");
        return roles!.Single(role => role.RoleCode == code).RoleId;
    }

    private static object UserBody(
        string username,
        string category,
        long roleId,
        long? secondRoleId = null,
        long? employeeId = null,
        long? customerId = null,
        long? branchId = null)
    {
        var roles = secondRoleId is > 0 ? new[] { roleId, secondRoleId.Value } : new[] { roleId };
        return new
        {
            username,
            password = ApiFactory.Password,
            firstName = "Silk",
            lastName = "User",
            userCategory = category,
            employeeId,
            customerId,
            branchId,
            roleIds = roles
        };
    }

    private sealed class UserItem
    {
        public long UserId { get; set; }
        public string Username { get; set; } = string.Empty;
    }

    private sealed class RoleItem
    {
        public long RoleId { get; set; }
        public string RoleCode { get; set; } = string.Empty;
    }

    private sealed class MeItem
    {
        public List<string> Permissions { get; set; } = [];
    }

    private sealed class Page<T>
    {
        public List<T> Items { get; set; } = [];
    }
}
