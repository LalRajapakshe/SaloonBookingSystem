using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Infrastructure.Authentication;
using SalonBooking.Persistence.Context;

namespace SalonBooking.API.Tests;

[Collection(ApiCollection.Name)]
public class AccessControlTests
{
    private readonly ApiFactory _factory;

    public AccessControlTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_token_carries_category_and_not_a_client_permission_list()
    {
        var login = await _factory.LoginAsync(ApiFactory.BranchUserAName);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken);

        Assert.Equal(nameof(UserCategory.General), Claim(token, "UserCategory"));
        Assert.DoesNotContain(token.Claims, claim => claim.Type == "permission" || claim.Type == "permissions");
        Assert.Equal("Staff", Claim(token, ClaimTypes.Role));
    }

    [Fact]
    public async Task Same_username_is_allowed_in_another_tenant_and_rejected_in_the_same_tenant()
    {
        using var adminA = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        await EnsureTenantAdminAsync("tenant.b.admin", _factory.TenantBId);
        using var adminB = await _factory.CreateAuthenticatedClientAsync("tenant.b.admin");

        var staffB = await RoleIdAsync(adminB, "STAFF");
        var createdB = await adminB.PostAsJsonAsync("/api/Users", UserBody("shared.name", "Password1!", "General", staffB, branchId: _factory.BranchB1Id));
        Assert.Equal(HttpStatusCode.OK, createdB.StatusCode);

        var staffA = await RoleIdAsync(adminA, "STAFF");
        var createdA = await adminA.PostAsJsonAsync("/api/Users", UserBody("shared.name", "Password1!", "General", staffA, branchId: _factory.BranchA1Id));
        Assert.Equal(HttpStatusCode.OK, createdA.StatusCode);

        var duplicate = await adminA.PostAsJsonAsync("/api/Users", UserBody("shared.name", "Password1!", "General", staffA, branchId: _factory.BranchA1Id));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        var body = await duplicate.Content.ReadAsStringAsync();
        Assert.Contains("username", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Employee_and_customer_links_must_match_the_category_and_tenant()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        var staff = await RoleIdAsync(admin, "STAFF");

        var missingEmployee = await admin.PostAsJsonAsync("/api/Users", UserBody("no.employee", "Password1!", "Employee", staff));
        Assert.Equal(HttpStatusCode.BadRequest, missingEmployee.StatusCode);

        var generalWithEmployee = await admin.PostAsJsonAsync(
            "/api/Users",
            UserBody("general.linked", "Password1!", "General", staff, employeeId: _factory.EmployeeA1Id));
        Assert.Equal(HttpStatusCode.BadRequest, generalWithEmployee.StatusCode);

        var crossTenant = await admin.PostAsJsonAsync(
            "/api/Users",
            UserBody("cross.employee", "Password1!", "Employee", staff, employeeId: _factory.EmployeeB1Id));
        Assert.Equal(HttpStatusCode.BadRequest, crossTenant.StatusCode);

        var employeeUser = await admin.PostAsJsonAsync(
            "/api/Users",
            UserBody("stylist.a", "Password1!", "Employee", staff, employeeId: _factory.EmployeeA1Id));
        Assert.Equal(HttpStatusCode.OK, employeeUser.StatusCode);
        var created = await employeeUser.Content.ReadFromJsonAsync<UserItem>();
        Assert.Equal(_factory.EmployeeA1Id, created!.EmployeeId);
        Assert.Equal(_factory.BranchA1Id, created.BranchId);
        Assert.Equal(nameof(UserCategory.Employee), created.UserCategory);
        Assert.Null(created.CustomerId);
    }

    [Fact]
    public async Task Customer_user_cannot_call_staff_endpoints_or_substitute_another_customer()
    {
        using (var admin = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName))
        {
            var customerRole = await RoleIdAsync(admin, "CUSTOMER");
            var created = await admin.PostAsJsonAsync(
                "/api/Users",
                UserBody("customer.access", "Password1!", "Customer", customerRole, customerId: _factory.CustomerA1Id));
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        }

        var login = await _factory.LoginAsync("customer.access");
        var token = new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken);
        Assert.Equal(_factory.CustomerA1Id.ToString(), Claim(token, "CustomerId"));
        Assert.DoesNotContain(token.Claims, claim => claim.Type == "EmployeeId");

        using var client = await _factory.CreateAuthenticatedClientAsync("customer.access");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/Employees")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/Appointments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/Customers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/Customers/{_factory.CustomerA2Id}")).StatusCode);

        var me = await client.GetFromJsonAsync<MeItem>("/api/Auth/me");
        Assert.Equal(_factory.CustomerA1Id, me!.CustomerId);
        Assert.DoesNotContain("employees.read", me.Permissions);
    }

    [Fact]
    public async Task Deactivated_user_cannot_log_in_and_an_existing_token_is_rejected()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        var staff = await RoleIdAsync(admin, "STAFF");
        var created = await admin.PostAsJsonAsync("/api/Users", UserBody("temp.staff", "Password1!", "General", staff, branchId: _factory.BranchA1Id));
        var user = await created.Content.ReadFromJsonAsync<UserItem>();

        var activeClient = await _factory.CreateAuthenticatedClientAsync("temp.staff");
        Assert.Equal(HttpStatusCode.OK, (await activeClient.GetAsync("/api/Employees")).StatusCode);

        var deactivated = await admin.PostAsJsonAsync($"/api/Users/{user!.UserId}/activation", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await activeClient.GetAsync("/api/Employees")).StatusCode);

        using var anonymous = _factory.CreateClient();
        var login = await anonymous.PostAsJsonAsync("/api/Auth/login", new { username = "temp.staff", password = "Password1!" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Last_user_manager_cannot_be_deactivated()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        var me = await admin.GetFromJsonAsync<MeItem>("/api/Auth/me");
        var response = await admin.PostAsJsonAsync($"/api/Users/{me!.UserId}/activation", new { isActive = false });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var stillActive = await _factory.LoginAsync(ApiFactory.TenantUserAName);
        Assert.False(string.IsNullOrWhiteSpace(stillActive.AccessToken));
    }

    [Fact]
    public async Task Staff_cannot_list_users_and_an_admin_cannot_see_another_tenant()
    {
        using var branch = await _factory.CreateAuthenticatedClientAsync(ApiFactory.BranchUserAName);
        Assert.Equal(HttpStatusCode.Forbidden, (await branch.GetAsync("/api/Users")).StatusCode);

        await EnsureTenantAdminAsync("tenant.b.reader", _factory.TenantBId);
        using var otherTenant = await _factory.CreateAuthenticatedClientAsync("tenant.b.reader");
        var users = await otherTenant.GetFromJsonAsync<Page<UserItem>>("/api/Users");
        Assert.NotNull(users);
        Assert.DoesNotContain(users!.Items, item => item.Username == ApiFactory.TenantUserAName);
    }

    [Fact]
    public async Task Initial_admin_provisioning_does_not_reset_an_existing_password()
    {
        var hasher = new PasswordHasher<User>();
        var before = await _factory.ReadAsync(db => db.Users.IgnoreQueryFilters().SingleAsync(user => user.Username == ApiFactory.TenantUserAName));
        var originalHash = before.PasswordHash;

        await _factory.ReadAsync(async db =>
        {
            await AccessControlBootstrap.ProvisionInitialAdminAsync(
                db,
                hasher,
                ApiFactory.TenantUserAName,
                "A-different-password-1",
                "TEN-A",
                NullLogger.Instance,
                CancellationToken.None);
            return 0;
        });

        var after = await _factory.ReadAsync(db => db.Users.IgnoreQueryFilters().SingleAsync(user => user.Username == ApiFactory.TenantUserAName));
        Assert.Equal(originalHash, after.PasswordHash);
        Assert.Equal(UserCategory.General, after.UserCategory);

        var login = await _factory.LoginAsync(ApiFactory.TenantUserAName);
        Assert.False(string.IsNullOrWhiteSpace(login.AccessToken));
    }

    [Fact]
    public async Task Inactive_built_in_role_is_not_granted_permissions_or_effective_access()
    {
        var hasher = new PasswordHasher<User>();
        var seeded = await _factory.ReadAsync(async db =>
        {
            var tenant = await AddTenantAsync(db, "TEN-INACT", "Inactive Role Salon");
            var branch = await AddBranchAsync(db, tenant.TenantId, "BRN-INACT", "Inactive Branch");
            await AccessControlBootstrap.EnsureTenantRolesAsync(db, tenant.TenantId);
            var adminRole = await BuiltInRoleAsync(db, tenant.TenantId, "ADMIN");
            var removed = await db.RolePermissions
                .Where(item => item.RoleId == adminRole.RoleId)
                .Join(db.Permissions, link => link.PermissionId, permission => permission.PermissionId, (link, permission) => new { link, permission })
                .SingleAsync(item => item.permission.PermissionCode == "users.manage");
            db.RolePermissions.Remove(removed.link);
            adminRole.IsActive = false;
            adminRole.RoleName = "Intentionally inactive admin";

            var custom = new Role
            {
                TenantId = tenant.TenantId,
                RoleCode = "RECEPTION",
                RoleName = "Reception",
                Description = "Leave this custom role alone"
            };
            db.Roles.Add(custom);
            await db.SaveChangesAsync();
            custom.IsActive = false;
            await db.SaveChangesAsync();

            var user = NewAccessUser(tenant.TenantId, branch.BranchId, "inactive.role.admin", "Admin", hasher);
            var hash = user.PasswordHash;
            db.Users.Add(user);
            await db.SaveChangesAsync();
            db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = adminRole.RoleId });
            await db.SaveChangesAsync();
            return new InactiveRoleSeed(adminRole.RoleId, custom.RoleId, user.UserId, hash);
        });

        await _factory.ReadAsync(async db =>
        {
            await AccessControlBootstrap.EnsureTenantRolesAsync(db, await TenantIdOfRoleAsync(db, seeded.AdminRoleId));
            return 0;
        });

        var afterProvisioning = await _factory.ReadAsync(async db =>
        {
            var adminRole = await db.Roles.IgnoreQueryFilters().SingleAsync(role => role.RoleId == seeded.AdminRoleId);
            var custom = await db.Roles.IgnoreQueryFilters().SingleAsync(role => role.RoleId == seeded.CustomRoleId);
            var user = await db.Users.IgnoreQueryFilters().SingleAsync(item => item.UserId == seeded.UserId);
            var manages = await db.RolePermissions.CountAsync(item =>
                item.RoleId == adminRole.RoleId &&
                db.Permissions.Any(permission => permission.PermissionId == item.PermissionId && permission.PermissionCode == "users.manage"));
            return new
            {
                adminRole.IsActive,
                adminRole.RoleName,
                manages,
                custom.RoleCode,
                CustomActive = custom.IsActive,
                custom.Description,
                user.PasswordHash
            };
        });

        Assert.False(afterProvisioning.IsActive);
        Assert.Equal("Intentionally inactive admin", afterProvisioning.RoleName);
        Assert.Equal(0, afterProvisioning.manages);
        Assert.Equal("RECEPTION", afterProvisioning.RoleCode);
        Assert.False(afterProvisioning.CustomActive);
        Assert.Equal("Leave this custom role alone", afterProvisioning.Description);
        Assert.Equal(seeded.PasswordHash, afterProvisioning.PasswordHash);

        var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _factory.ReadAsync(async db =>
            {
                var user = await db.Users.IgnoreQueryFilters().SingleAsync(item => item.UserId == seeded.UserId);
                await AccessControlBootstrap.AssignLegacyRoleAsync(db, user);
                return 0;
            }));
        Assert.Contains("inactive", blocked.Message, StringComparison.OrdinalIgnoreCase);

        using var client = await _factory.CreateAuthenticatedClientAsync("inactive.role.admin");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/Users")).StatusCode);
        var stillLinked = await _factory.ReadAsync(db => db.UserRoles.AnyAsync(item => item.UserId == seeded.UserId && item.RoleId == seeded.AdminRoleId));
        Assert.True(stillLinked);
    }

    [Fact]
    public async Task Deleted_built_in_role_is_replaced_by_an_active_role_that_authorizes()
    {
        var hasher = new PasswordHasher<User>();
        var seeded = await _factory.ReadAsync(async db =>
        {
            var tenant = await AddTenantAsync(db, "TEN-DELROLE", "Deleted Role Salon");
            var otherTenant = await AddTenantAsync(db, "TEN-OTHER", "Other Salon");
            var branch = await AddBranchAsync(db, tenant.TenantId, "BRN-DELROLE", "Deleted Role Branch");
            await AccessControlBootstrap.EnsureTenantRolesAsync(db, tenant.TenantId);
            await AccessControlBootstrap.EnsureTenantRolesAsync(db, otherTenant.TenantId);
            var staff = await BuiltInRoleAsync(db, tenant.TenantId, "STAFF");
            staff.IsDeleted = true;
            staff.RoleName = "Retired staff";

            var otherCustom = new Role
            {
                TenantId = otherTenant.TenantId,
                RoleCode = "RECEPTION",
                RoleName = "Other reception",
                Description = "Other tenant"
            };
            db.Roles.Add(otherCustom);
            await db.SaveChangesAsync();
            otherCustom.IsActive = false;
            var user = NewAccessUser(tenant.TenantId, branch.BranchId, "deleted.role.staff", "Staff", hasher);
            var hash = user.PasswordHash;
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return new DeletedRoleSeed(tenant.TenantId, otherTenant.TenantId, staff.RoleId, otherCustom.RoleId, user.UserId, hash);
        });

        await _factory.ReadAsync(async db =>
        {
            var user = await db.Users.IgnoreQueryFilters().SingleAsync(item => item.UserId == seeded.UserId);
            await AccessControlBootstrap.AssignLegacyRoleAsync(db, user);
            return 0;
        });

        var after = await _factory.ReadAsync(async db =>
        {
            var retired = await db.Roles.IgnoreQueryFilters().SingleAsync(role => role.RoleId == seeded.RetiredRoleId);
            var replacement = await db.Roles.IgnoreQueryFilters().SingleAsync(role =>
                role.TenantId == seeded.TenantId && role.RoleCode == "STAFF" && !role.IsDeleted);
            var otherCustom = await db.Roles.IgnoreQueryFilters().SingleAsync(role => role.RoleId == seeded.OtherCustomRoleId);
            var otherStaff = await db.Roles.IgnoreQueryFilters().SingleAsync(role =>
                role.TenantId == seeded.OtherTenantId && role.RoleCode == "STAFF" && !role.IsDeleted);
            var user = await db.Users.IgnoreQueryFilters().SingleAsync(item => item.UserId == seeded.UserId);
            var linkedToReplacement = await db.UserRoles.AnyAsync(item => item.UserId == user.UserId && item.RoleId == replacement.RoleId);
            var linkedToRetired = await db.UserRoles.AnyAsync(item => item.UserId == user.UserId && item.RoleId == retired.RoleId);
            var canReadAppointments = await db.RolePermissions.AnyAsync(item =>
                item.RoleId == replacement.RoleId &&
                db.Permissions.Any(permission =>
                    permission.PermissionId == item.PermissionId &&
                    permission.PermissionCode == "appointments.read" &&
                    permission.IsActive &&
                    !permission.IsDeleted));
            return new
            {
                retired.IsDeleted,
                RetiredName = retired.RoleName,
                replacement.IsActive,
                replacement.RoleId,
                linkedToReplacement,
                linkedToRetired,
                canReadAppointments,
                otherCustom.Description,
                OtherCustomActive = otherCustom.IsActive,
                OtherStaffId = otherStaff.RoleId,
                user.PasswordHash
            };
        });

        Assert.True(after.IsDeleted);
        Assert.Equal("Retired staff", after.RetiredName);
        Assert.True(after.IsActive);
        Assert.NotEqual(seeded.RetiredRoleId, after.RoleId);
        Assert.True(after.linkedToReplacement);
        Assert.False(after.linkedToRetired);
        Assert.True(after.canReadAppointments);
        Assert.Equal("Other tenant", after.Description);
        Assert.False(after.OtherCustomActive);
        Assert.NotEqual(after.RoleId, after.OtherStaffId);
        Assert.Equal(seeded.PasswordHash, after.PasswordHash);

        using var client = await _factory.CreateAuthenticatedClientAsync("deleted.role.staff");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/Employees")).StatusCode);
    }

    [Fact]
    public void Migration_script_matches_the_inactive_built_in_role_policy()
    {
        var migration = File.ReadAllText(FindWorkspaceFile(Path.Combine(
            "src", "SalonBooking.Persistence", "Migrations", "20261015015246_AddUserAccessFoundation.cs")));
        var script = File.ReadAllText(FindWorkspaceFile(Path.Combine("scripts", "AddUserAccessFoundation.sql")));
        string[] markers =
        [
            "THROW 50029",
            "will not reactivate it",
            "AND r.[IsActive] = 1",
            "THROW 50035",
            "THROW 50036",
            "users.manage",
            "appointments.read"
        ];

        foreach (var marker in markers)
        {
            Assert.Contains(marker, migration, StringComparison.Ordinal);
            Assert.Contains(marker, script, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Scheduling_still_rejects_a_branch_user_creating_in_another_branch()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync(ApiFactory.BranchUserAName);
        var response = await client.PostAsJsonAsync("/api/Appointments", new
        {
            branchId = _factory.BranchA2Id,
            customerId = _factory.CustomerA1Id,
            appointmentDate = "2026-10-20"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<Tenant> AddTenantAsync(SalonBookingDbContext db, string code, string name)
    {
        var tenant = new Tenant
        {
            TenantCode = code,
            TenantName = name,
            BusinessName = name,
            SubscriptionPlan = SubscriptionPlan.Trial,
            SubscriptionStartDate = DateTime.UtcNow,
            SubscriptionEndDate = DateTime.UtcNow.AddYears(1),
            MaxBranches = 5,
            MaxUsers = 20
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    private static async Task<Branch> AddBranchAsync(SalonBookingDbContext db, long tenantId, string code, string name)
    {
        var branch = new Branch
        {
            TenantId = tenantId,
            BranchCode = code,
            BranchName = name,
            IsHeadOffice = true
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        return branch;
    }

    private static async Task<Role> BuiltInRoleAsync(SalonBookingDbContext db, long tenantId, string code)
    {
        return await db.Roles.IgnoreQueryFilters().SingleAsync(role =>
            role.TenantId == tenantId && role.RoleCode == code && !role.IsDeleted);
    }

    private static async Task<long> TenantIdOfRoleAsync(SalonBookingDbContext db, long roleId)
    {
        return await db.Roles.IgnoreQueryFilters()
            .Where(role => role.RoleId == roleId)
            .Select(role => role.TenantId)
            .SingleAsync();
    }

    private static User NewAccessUser(long tenantId, long branchId, string username, string role, PasswordHasher<User> hasher)
    {
        var user = new User
        {
            TenantId = tenantId,
            BranchId = branchId,
            Username = username,
            FirstName = "Access",
            LastName = "Test",
            Role = role,
            UserCategory = UserCategory.General,
            IsActive = true
        };
        user.PasswordHash = hasher.HashPassword(user, ApiFactory.Password);
        return user;
    }

    private static string FindWorkspaceFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Workspace file was not found.", relativePath);
    }

    private async Task EnsureTenantAdminAsync(string username, long tenantId)
    {
        var hasher = new PasswordHasher<User>();
        await _factory.ReadAsync(async db =>
        {
            var existing = await db.Users.IgnoreQueryFilters()
                .AnyAsync(user => user.TenantId == tenantId && user.Username == username && !user.IsDeleted);
            if (existing)
            {
                return 0;
            }

            var user = new User
            {
                TenantId = tenantId,
                Username = username,
                FirstName = "Tenant",
                LastName = "Admin",
                Role = "Admin",
                UserCategory = UserCategory.General,
                IsActive = true
            };
            user.PasswordHash = hasher.HashPassword(user, ApiFactory.Password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            await AccessControlBootstrap.AssignLegacyRoleAsync(db, user);
            return 0;
        });
    }

    private async Task<long> RoleIdAsync(HttpClient client, string code)
    {
        var roles = await client.GetFromJsonAsync<List<RoleItem>>("/api/Roles");
        return roles!.Single(role => role.RoleCode == code).RoleId;
    }

    private static object UserBody(
        string username,
        string password,
        string category,
        long roleId,
        long? employeeId = null,
        long? customerId = null,
        long? branchId = null)
    {
        return new
        {
            username,
            password,
            firstName = "Test",
            lastName = "User",
            userCategory = category,
            employeeId,
            customerId,
            branchId,
            roleIds = new[] { roleId }
        };
    }

    private static string Claim(JwtSecurityToken token, string type)
    {
        return token.Claims.Single(claim => claim.Type == type).Value;
    }

    private sealed class UserItem
    {
        public long UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string UserCategory { get; set; } = string.Empty;
        public long? BranchId { get; set; }
        public long? EmployeeId { get; set; }
        public long? CustomerId { get; set; }
    }

    private sealed class RoleItem
    {
        public long RoleId { get; set; }
        public string RoleCode { get; set; } = string.Empty;
    }

    private sealed class MeItem
    {
        public long UserId { get; set; }
        public long? CustomerId { get; set; }
        public List<string> Permissions { get; set; } = [];
    }

    private sealed class Page<T>
    {
        public List<T> Items { get; set; } = [];
    }

    private sealed record InactiveRoleSeed(long AdminRoleId, long CustomRoleId, long UserId, string PasswordHash);

    private sealed record DeletedRoleSeed(
        long TenantId,
        long OtherTenantId,
        long RetiredRoleId,
        long OtherCustomRoleId,
        long UserId,
        string PasswordHash);
}
