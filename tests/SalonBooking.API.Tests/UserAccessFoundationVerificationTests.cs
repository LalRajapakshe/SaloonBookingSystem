using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Infrastructure.Authentication;

namespace SalonBooking.API.Tests;

[Collection(ApiCollection.Name)]
public class UserAccessFoundationVerificationTests
{
    private const string TestSigningKey = "test-signing-key-0123456789-test-signing-key";

    private readonly ApiFactory _factory;

    public UserAccessFoundationVerificationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Account_links_are_unique_and_match_the_category()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        var staff = await RoleIdAsync(admin, "STAFF");
        var customerRole = await RoleIdAsync(admin, "CUSTOMER");
        var employeeId = await AddEmployeeAsync("EMP-LINK");
        var customerId = await AddCustomerAsync("CUS-LINK");

        var general = await admin.PostAsJsonAsync("/api/Users", UserBody("link.general", "General", staff, branchId: _factory.BranchA1Id));
        var generalBody = await general.Content.ReadFromJsonAsync<UserItem>();
        Assert.Equal(HttpStatusCode.OK, general.StatusCode);
        Assert.Equal(nameof(UserCategory.General), generalBody!.UserCategory);
        Assert.Null(generalBody.EmployeeId);
        Assert.Null(generalBody.CustomerId);

        var employee = await admin.PostAsJsonAsync("/api/Users", UserBody("link.employee", "Employee", staff, employeeId: employeeId));
        var employeeBody = await employee.Content.ReadFromJsonAsync<UserItem>();
        Assert.Equal(HttpStatusCode.OK, employee.StatusCode);
        Assert.Equal(employeeId, employeeBody!.EmployeeId);
        Assert.Null(employeeBody.CustomerId);
        Assert.Equal(_factory.BranchA1Id, employeeBody.BranchId);

        var secondEmployee = await admin.PostAsJsonAsync("/api/Users", UserBody("link.employee.2", "Employee", staff, employeeId: employeeId));
        Assert.Equal(HttpStatusCode.BadRequest, secondEmployee.StatusCode);
        Assert.Contains("employee", await secondEmployee.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var withBoth = await admin.PostAsJsonAsync(
            "/api/Users",
            UserBody("link.both", "Employee", staff, employeeId: employeeId, customerId: customerId));
        Assert.Equal(HttpStatusCode.BadRequest, withBoth.StatusCode);

        var customer = await admin.PostAsJsonAsync("/api/Users", UserBody("link.customer", "Customer", customerRole, customerId: customerId));
        var customerBody = await customer.Content.ReadFromJsonAsync<UserItem>();
        Assert.Equal(HttpStatusCode.OK, customer.StatusCode);
        Assert.Equal(customerId, customerBody!.CustomerId);
        Assert.Null(customerBody.EmployeeId);

        var secondCustomer = await admin.PostAsJsonAsync("/api/Users", UserBody("link.customer.2", "Customer", customerRole, customerId: customerId));
        Assert.Equal(HttpStatusCode.BadRequest, secondCustomer.StatusCode);
        Assert.Contains("customer", await secondCustomer.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var missingCustomer = await admin.PostAsJsonAsync("/api/Users", UserBody("link.customer.missing", "Customer", customerRole));
        Assert.Equal(HttpStatusCode.BadRequest, missingCustomer.StatusCode);

        var crossCustomer = await admin.PostAsJsonAsync(
            "/api/Users",
            UserBody("link.customer.cross", "Customer", customerRole, customerId: _factory.CustomerB1Id));
        Assert.Equal(HttpStatusCode.BadRequest, crossCustomer.StatusCode);
    }

    [Fact]
    public async Task Created_user_keeps_its_links_password_hash_and_validation_after_reload()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        var staff = await RoleIdAsync(admin, "STAFF");
        var created = await admin.PostAsJsonAsync("/api/Users", UserBody("reload.user", "General", staff, branchId: _factory.BranchA2Id));
        var createdBody = await created.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.DoesNotContain("passwordHash", createdBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password1!", createdBody, StringComparison.Ordinal);
        var user = await created.Content.ReadFromJsonAsync<UserItem>();

        using var reloadedClient = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        var reloaded = await reloadedClient.GetAsync($"/api/Users/{user!.UserId}");
        var reloadedBody = await reloaded.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, reloaded.StatusCode);
        Assert.Contains("reload.user", reloadedBody, StringComparison.Ordinal);
        Assert.Contains(nameof(UserCategory.General), reloadedBody, StringComparison.Ordinal);
        Assert.DoesNotContain("passwordHash", reloadedBody, StringComparison.OrdinalIgnoreCase);

        var stored = await _factory.ReadAsync(db => db.Users.IgnoreQueryFilters().SingleAsync(item => item.UserId == user.UserId));
        Assert.NotEqual("Password1!", stored.PasswordHash);
        Assert.StartsWith("AQAAAA", stored.PasswordHash, StringComparison.Ordinal);
        Assert.Equal(_factory.TenantAId, stored.TenantId);
        Assert.Equal(_factory.BranchA2Id, stored.BranchId);
        Assert.Null(stored.EmployeeId);
        Assert.Null(stored.CustomerId);

        var listed = await reloadedClient.GetAsync("/api/Users?search=reload.user");
        var listedBody = await listed.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.Contains("reload.user", listedBody, StringComparison.Ordinal);
        Assert.DoesNotContain("passwordHash", listedBody, StringComparison.OrdinalIgnoreCase);

        var updated = await admin.PutAsJsonAsync($"/api/Users/{user.UserId}", UserBody("reload.user", "General", staff, branchId: _factory.BranchA2Id, password: "Password2!", firstName: "Reloaded"));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var after = await _factory.ReadAsync(db => db.Users.IgnoreQueryFilters().SingleAsync(item => item.UserId == user.UserId));
        Assert.Equal("Reloaded", after.FirstName);
        Assert.NotEqual(stored.PasswordHash, after.PasswordHash);
        Assert.DoesNotContain("Password2!", after.PasswordHash, StringComparison.Ordinal);

        using var anonymous = _factory.CreateClient();
        var oldLogin = await anonymous.PostAsJsonAsync("/api/Auth/login", new { username = "reload.user", password = "Password1!" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        var newLogin = await anonymous.PostAsJsonAsync("/api/Auth/login", new { username = "reload.user", password = "Password2!" });
        var loginBody = await newLogin.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
        Assert.DoesNotContain("passwordHash", loginBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password2!", loginBody, StringComparison.Ordinal);

        var shortPassword = await admin.PostAsJsonAsync("/api/Users", UserBody("reload.short", "General", staff, password: "short", branchId: _factory.BranchA1Id));
        Assert.Equal(HttpStatusCode.BadRequest, shortPassword.StatusCode);
        var longName = await admin.PostAsJsonAsync("/api/Users", UserBody(new string('u', 101), "General", staff, branchId: _factory.BranchA1Id));
        Assert.Equal(HttpStatusCode.BadRequest, longName.StatusCode);
    }

    [Fact]
    public async Task Supplied_tenant_and_foreign_branch_cannot_move_a_user()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        var staff = await RoleIdAsync(admin, "STAFF");
        var response = await admin.PostAsJsonAsync("/api/Users", new
        {
            username = "tenant.bound",
            password = "Password1!",
            firstName = "Test",
            lastName = "User",
            userCategory = "General",
            tenantId = _factory.TenantBId,
            branchId = _factory.BranchB1Id,
            roleIds = new[] { staff }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var stored = await _factory.ReadAsync(db => db.Users.IgnoreQueryFilters().AnyAsync(user => user.Username == "tenant.bound"));
        Assert.False(stored);
    }

    [Fact]
    public async Task Branch_manager_cannot_read_or_place_users_outside_that_branch()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        var adminRole = await RoleIdAsync(admin, "ADMIN");
        var createdManagerIds = new List<long>();
        try
        {
            var outside = await admin.PostAsJsonAsync("/api/Users", UserBody("branch.outside", "General", adminRole, branchId: _factory.BranchA2Id));
            var outsideUser = await outside.Content.ReadFromJsonAsync<UserItem>();
            createdManagerIds.Add(outsideUser!.UserId);
            Assert.Equal(HttpStatusCode.OK, outside.StatusCode);

            var manager = await admin.PostAsJsonAsync("/api/Users", UserBody("branch.manager", "General", adminRole, branchId: _factory.BranchA1Id));
            var managerUser = await manager.Content.ReadFromJsonAsync<UserItem>();
            createdManagerIds.Add(managerUser!.UserId);
            Assert.Equal(HttpStatusCode.OK, manager.StatusCode);
            using var branchAdmin = await _factory.CreateAuthenticatedClientAsync("branch.manager");

            var list = await branchAdmin.GetFromJsonAsync<Page<UserItem>>("/api/Users?pageSize=100");
            Assert.NotNull(list);
            Assert.DoesNotContain(list!.Items, item => item.Username == "branch.outside");
            Assert.Contains(list.Items, item => item.Username == "branch.manager");

            Assert.Equal(HttpStatusCode.Forbidden, (await branchAdmin.GetAsync($"/api/Users/{outsideUser.UserId}")).StatusCode);

            var placeOutside = await branchAdmin.PostAsJsonAsync("/api/Users", UserBody("branch.escape", "General", adminRole, branchId: _factory.BranchA2Id));
            Assert.Equal(HttpStatusCode.BadRequest, placeOutside.StatusCode);

            var forced = await branchAdmin.PostAsJsonAsync("/api/Users", UserBody("branch.forced", "General", adminRole));
            var forcedUser = await forced.Content.ReadFromJsonAsync<UserItem>();
            createdManagerIds.Add(forcedUser!.UserId);
            Assert.Equal(HttpStatusCode.OK, forced.StatusCode);
            Assert.Equal(_factory.BranchA1Id, forcedUser.BranchId);
        }
        finally
        {
            foreach (var userId in createdManagerIds)
            {
                await admin.PostAsJsonAsync($"/api/Users/{userId}/activation", new { isActive = false });
            }
        }

        var otherUserId = await _factory.ReadAsync(db =>
            db.Users.IgnoreQueryFilters().Where(user => user.Username == ApiFactory.BranchUserBName).Select(user => user.UserId).SingleAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/Users/{otherUserId}")).StatusCode);

        var foreignRole = await _factory.ReadAsync(db =>
            db.Roles.IgnoreQueryFilters().Where(role => role.TenantId == _factory.TenantBId && role.RoleCode == "STAFF" && !role.IsDeleted).Select(role => role.RoleId).SingleAsync());
        var foreignRoleUser = await admin.PostAsJsonAsync("/api/Users", UserBody("foreign.role", "General", foreignRole, branchId: _factory.BranchA1Id));
        Assert.Equal(HttpStatusCode.BadRequest, foreignRoleUser.StatusCode);
    }

    [Fact]
    public async Task Direct_calls_without_a_screen_still_require_the_server_permission()
    {
        using var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/Users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/Roles")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/Permissions")).StatusCode);

        using var staff = await _factory.CreateAuthenticatedClientAsync(ApiFactory.BranchUserAName);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/Users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/Roles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PutAsJsonAsync("/api/Users/1/roles", new { roleIds = Array.Empty<long>() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PutAsJsonAsync("/api/Roles/1/permissions", new { permissionIds = Array.Empty<long>() })).StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_call_staff_catalogs_and_a_forged_category_claim_does_not_help()
    {
        using (var admin = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName))
        {
            var customerRole = await RoleIdAsync(admin, "CUSTOMER");
            var customerId = await AddCustomerAsync("CUS-DENY");
            var created = await admin.PostAsJsonAsync("/api/Users", UserBody("customer.denied", "Customer", customerRole, customerId: customerId));
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        }

        var login = await _factory.LoginAsync("customer.denied");
        using var customer = ClientWith(login.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/Users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/Roles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/Permissions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/Branch")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/Tenant")).StatusCode);

        using var forged = ClientWith(Resign(login.AccessToken, new Dictionary<string, string?> { ["UserCategory"] = nameof(UserCategory.General) }));
        Assert.Equal(HttpStatusCode.Forbidden, (await forged.GetAsync("/api/Employees")).StatusCode);
    }

    [Fact]
    public async Task Customer_role_rejects_staff_permissions_and_a_later_grant_still_cannot_call_staff_apis()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        var customerRole = await RoleIdAsync(admin, "CUSTOMER");
        var employeesRead = await PermissionIdAsync(admin, "employees.read");
        var profileRead = await PermissionIdAsync(admin, "customer.profile.read");

        var builtIn = await admin.PutAsJsonAsync($"/api/Roles/{customerRole}/permissions", new { permissionIds = new[] { profileRead, employeesRead } });
        Assert.Equal(HttpStatusCode.BadRequest, builtIn.StatusCode);

        var createdRole = await admin.PostAsJsonAsync("/api/Roles", new { roleCode = "PORTAL", roleName = "Portal", description = "Customer portal", isActive = true });
        var portal = await createdRole.Content.ReadFromJsonAsync<RoleItem>();
        Assert.Equal(HttpStatusCode.OK, createdRole.StatusCode);
        var initialPermissions = await admin.PutAsJsonAsync($"/api/Roles/{portal!.RoleId}/permissions", new { permissionIds = new[] { profileRead } });
        Assert.Equal(HttpStatusCode.OK, initialPermissions.StatusCode);

        var customerId = await AddCustomerAsync("CUS-PORTAL");
        var createdUser = await admin.PostAsJsonAsync("/api/Users", UserBody("customer.portal", "Customer", portal.RoleId, customerId: customerId));
        Assert.Equal(HttpStatusCode.OK, createdUser.StatusCode);

        var elevated = await admin.PutAsJsonAsync($"/api/Roles/{portal.RoleId}/permissions", new { permissionIds = new[] { profileRead, employeesRead } });
        Assert.Equal(HttpStatusCode.BadRequest, elevated.StatusCode);

        using var customer = await _factory.CreateAuthenticatedClientAsync("customer.portal");
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync($"/api/Employees/{_factory.EmployeeA1Id}")).StatusCode);
    }

    [Fact]
    public async Task Customer_profile_does_not_advertise_staff_permissions_added_after_assignment()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        var profileRead = await PermissionIdAsync(admin, "customer.profile.read");
        var employeesRead = await PermissionIdAsync(admin, "employees.read");
        var createdRole = await admin.PostAsJsonAsync("/api/Roles", new { roleCode = "PORTAL2", roleName = "Portal Two", description = "Customer portal", isActive = true });
        var portal = await createdRole.Content.ReadFromJsonAsync<RoleItem>();
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/Roles/{portal!.RoleId}/permissions", new { permissionIds = new[] { profileRead } })).StatusCode);
        var customerId = await AddCustomerAsync("CUS-PORTAL2");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/Users", UserBody("customer.portal2", "Customer", portal.RoleId, customerId: customerId))).StatusCode);
        var elevated = await admin.PutAsJsonAsync($"/api/Roles/{portal.RoleId}/permissions", new { permissionIds = new[] { profileRead, employeesRead } });
        Assert.Equal(HttpStatusCode.BadRequest, elevated.StatusCode);

        await _factory.ReadAsync(async db =>
        {
            db.RolePermissions.Add(new RolePermission { RoleId = portal.RoleId, PermissionId = employeesRead });
            await db.SaveChangesAsync();
            return 0;
        });

        using var customer = await _factory.CreateAuthenticatedClientAsync("customer.portal2");
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync($"/api/Employees/{_factory.EmployeeA1Id}")).StatusCode);
        var me = await customer.GetFromJsonAsync<MeItem>("/api/Auth/me");
        Assert.Contains("customer.profile.read", me!.Permissions);
        Assert.DoesNotContain("employees.read", me.Permissions);
    }

    [Fact]
    public async Task Deactivated_token_is_rejected_on_the_profile_endpoint()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        var staff = await RoleIdAsync(admin, "STAFF");
        var created = await admin.PostAsJsonAsync("/api/Users", UserBody("deactivate.me", "General", staff, branchId: _factory.BranchA1Id));
        var user = await created.Content.ReadFromJsonAsync<UserItem>();
        var login = await _factory.LoginAsync("deactivate.me");
        using var client = ClientWith(login.AccessToken);

        var deactivated = await admin.PostAsJsonAsync($"/api/Users/{user!.UserId}/activation", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        var me = await client.GetAsync("/api/Auth/me");
        var body = await me.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Forbidden, me.StatusCode);
        Assert.Contains("inactive", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Role_and_permission_changes_apply_to_the_existing_token()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        var employeesRead = await PermissionIdAsync(admin, "employees.read");
        var createdRole = await admin.PostAsJsonAsync("/api/Roles", new { roleCode = "DESK", roleName = "Desk", description = "Front desk", isActive = true });
        var role = await createdRole.Content.ReadFromJsonAsync<RoleItem>();
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/Roles/{role!.RoleId}/permissions", new { permissionIds = new[] { employeesRead } })).StatusCode);

        var createdUser = await admin.PostAsJsonAsync("/api/Users", UserBody("desk.user", "General", role.RoleId, branchId: _factory.BranchA1Id));
        Assert.Equal(HttpStatusCode.OK, createdUser.StatusCode);
        using var desk = await _factory.CreateAuthenticatedClientAsync("desk.user");
        Assert.Equal(HttpStatusCode.OK, (await desk.GetAsync($"/api/Employees/{_factory.EmployeeA1Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/Roles/{role.RoleId}/permissions", new { permissionIds = Array.Empty<long>() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await desk.GetAsync($"/api/Employees/{_factory.EmployeeA1Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/Roles/{role.RoleId}/permissions", new { permissionIds = new[] { employeesRead } })).StatusCode);
        var paused = await admin.PutAsJsonAsync($"/api/Roles/{role.RoleId}", new { roleCode = "DESK", roleName = "Desk", description = "Paused", isActive = false });
        Assert.Equal(HttpStatusCode.OK, paused.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await desk.GetAsync($"/api/Employees/{_factory.EmployeeA1Id}")).StatusCode);

        var resumed = await admin.PutAsJsonAsync($"/api/Roles/{role.RoleId}", new { roleCode = "DESK", roleName = "Desk", description = "Resumed", isActive = true });
        Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await desk.GetAsync($"/api/Employees/{_factory.EmployeeA1Id}")).StatusCode);
    }

    [Fact]
    public async Task Inactive_permission_does_not_grant_access()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        var employeesRead = await PermissionIdAsync(admin, "employees.read");
        var createdRole = await admin.PostAsJsonAsync("/api/Roles", new { roleCode = "LOOKUP", roleName = "Lookup", description = "Lookup", isActive = true });
        var role = await createdRole.Content.ReadFromJsonAsync<RoleItem>();
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/Roles/{role!.RoleId}/permissions", new { permissionIds = new[] { employeesRead } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/Users", UserBody("lookup.user", "General", role.RoleId, branchId: _factory.BranchA1Id))).StatusCode);
        using var client = await _factory.CreateAuthenticatedClientAsync("lookup.user");

        try
        {
            await SetPermissionActiveAsync("employees.read", false);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/Employees/{_factory.EmployeeA1Id}")).StatusCode);
        }
        finally
        {
            await SetPermissionActiveAsync("employees.read", true);
        }

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/Employees/{_factory.EmployeeA1Id}")).StatusCode);
    }

    [Fact]
    public async Task Only_admin_role_cannot_lose_user_management_and_its_code_stays_fixed()
    {
        var username = await AddIsolatedAdminAsync("sole.admin");
        using var admin = await _factory.CreateAuthenticatedClientAsync(username);
        var roles = await admin.GetFromJsonAsync<List<RoleItem>>("/api/Roles");
        var adminRole = roles!.Single(role => role.RoleCode == "ADMIN");
        var permissions = await admin.GetFromJsonAsync<List<PermissionItem>>("/api/Permissions");
        var withoutManage = permissions!.Where(item => item.PermissionCode != "users.manage").Select(item => item.PermissionId).ToArray();

        var stripped = await admin.PutAsJsonAsync($"/api/Roles/{adminRole.RoleId}/permissions", new { permissionIds = withoutManage });
        Assert.Equal(HttpStatusCode.BadRequest, stripped.StatusCode);
        Assert.Contains("manage users", await stripped.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var renamed = await admin.PutAsJsonAsync($"/api/Roles/{adminRole.RoleId}", new
        {
            roleCode = "OWNER",
            roleName = "Owner",
            description = "Renamed",
            isActive = true
        });
        Assert.Equal(HttpStatusCode.BadRequest, renamed.StatusCode);
        var after = await admin.GetFromJsonAsync<List<RoleItem>>("/api/Roles");
        Assert.Contains(after!, role => role.RoleCode == "ADMIN");
    }

    [Fact]
    public async Task Duplicate_and_invalid_role_codes_are_rejected()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        var duplicate = await admin.PostAsJsonAsync("/api/Roles", new { roleCode = "STAFF", roleName = "Another staff", description = "Duplicate", isActive = true });
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        var invalid = await admin.PostAsJsonAsync("/api/Roles", new { roleCode = "BAD CODE", roleName = "Bad", description = "Bad", isActive = true });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Category_check_rejects_a_general_user_linked_to_an_employee()
    {
        var employeeId = await AddEmployeeAsync("EMP-CHECK");
        var saved = false;
        try
        {
            await _factory.ReadAsync(async db =>
            {
                db.Users.Add(new User
                {
                    TenantId = _factory.TenantAId,
                    UserCategory = UserCategory.General,
                    EmployeeId = employeeId,
                    Username = "check.general",
                    PasswordHash = "not-a-login-hash",
                    FirstName = "Check",
                    LastName = "User"
                });
                await db.SaveChangesAsync();
                return 0;
            });
            saved = true;
        }
        catch (DbUpdateException)
        {
            saved = false;
        }

        Assert.False(saved);
    }

    [Fact]
    public async Task Deleted_user_still_holding_an_employee_is_not_reported_as_a_username_collision()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        var staff = await RoleIdAsync(admin, "STAFF");
        var employeeId = await AddEmployeeAsync("EMP-DEL");
        var created = await admin.PostAsJsonAsync("/api/Users", UserBody("deleted.link", "Employee", staff, employeeId: employeeId));
        var user = await created.Content.ReadFromJsonAsync<UserItem>();
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        await _factory.ReadAsync(async db =>
        {
            var stored = await db.Users.IgnoreQueryFilters().SingleAsync(item => item.UserId == user!.UserId);
            stored.IsDeleted = true;
            await db.SaveChangesAsync();
            return 0;
        });

        var retry = await admin.PostAsJsonAsync("/api/Users", UserBody("deleted.link.2", "Employee", staff, employeeId: employeeId));
        var body = await retry.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, retry.StatusCode);
        Assert.Contains("employee", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("username", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rewritten_token_with_unchanged_claims_still_works_and_a_bad_signature_does_not()
    {
        var login = await _factory.LoginAsync(ApiFactory.BranchUserAName);
        using var same = ClientWith(Resign(login.AccessToken, new Dictionary<string, string?>()));
        Assert.Equal(HttpStatusCode.OK, (await same.GetAsync($"/api/Employees/{_factory.EmployeeA1Id}")).StatusCode);

        using var wrongKey = ClientWith(Resign(login.AccessToken, new Dictionary<string, string?>(), "wrong-signing-key-0123456789-wrong-signing"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await wrongKey.GetAsync($"/api/Employees/{_factory.EmployeeA1Id}")).StatusCode);

        using var flipped = ClientWith(FlipPayload(login.AccessToken));
        Assert.Equal(HttpStatusCode.Unauthorized, (await flipped.GetAsync($"/api/Employees/{_factory.EmployeeA1Id}")).StatusCode);
    }

    [Fact]
    public async Task Signed_token_with_substituted_tenant_cannot_read_another_tenant()
    {
        var login = await _factory.LoginAsync(ApiFactory.TenantUserAName);
        using var original = ClientWith(login.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, (await original.GetAsync($"/api/Employees/{_factory.EmployeeB1Id}")).StatusCode);

        using var swapped = ClientWith(Resign(login.AccessToken, new Dictionary<string, string?>
        {
            ["TenantId"] = _factory.TenantBId.ToString()
        }));
        var response = await swapped.GetAsync($"/api/Employees/{_factory.EmployeeB1Id}");
        Assert.True(
            response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Substituted tenant returned {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task Signed_token_with_substituted_branch_cannot_read_another_branch()
    {
        var login = await _factory.LoginAsync(ApiFactory.BranchUserAName);
        using var original = ClientWith(login.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await original.GetAsync($"/api/Employees/{_factory.EmployeeA2Id}")).StatusCode);

        using var swapped = ClientWith(Resign(login.AccessToken, new Dictionary<string, string?>
        {
            ["BranchId"] = _factory.BranchA2Id.ToString()
        }));
        var response = await swapped.GetAsync($"/api/Employees/{_factory.EmployeeA2Id}");
        Assert.True(
            response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Substituted branch returned {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task Existing_token_follows_a_branch_change_on_the_account()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        var staff = await RoleIdAsync(admin, "STAFF");
        var created = await admin.PostAsJsonAsync("/api/Users", UserBody("moving.user", "General", staff, branchId: _factory.BranchA1Id));
        var user = await created.Content.ReadFromJsonAsync<UserItem>();
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var login = await _factory.LoginAsync("moving.user");
        using var client = ClientWith(login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/Employees/{_factory.EmployeeA1Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/Employees/{_factory.EmployeeA2Id}")).StatusCode);

        await _factory.ReadAsync(async db =>
        {
            var stored = await db.Users.IgnoreQueryFilters().SingleAsync(item => item.UserId == user!.UserId);
            stored.BranchId = _factory.BranchA2Id;
            await db.SaveChangesAsync();
            return 0;
        });

        var oldBranch = await client.GetAsync($"/api/Employees/{_factory.EmployeeA1Id}");
        var newBranch = await client.GetAsync($"/api/Employees/{_factory.EmployeeA2Id}");
        Assert.True(
            oldBranch.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Old branch remained available: {(int)oldBranch.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, newBranch.StatusCode);
    }

    [Fact]
    public async Task Existing_salon_wide_token_is_restricted_when_a_branch_is_assigned()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        var staff = await RoleIdAsync(admin, "STAFF");
        var created = await admin.PostAsJsonAsync("/api/Users", UserBody("wide.user", "General", staff));
        var user = await created.Content.ReadFromJsonAsync<UserItem>();
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Null(user!.BranchId);
        var login = await _factory.LoginAsync("wide.user");
        using var client = ClientWith(login.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/Employees/{_factory.EmployeeA2Id}")).StatusCode);

        await _factory.ReadAsync(async db =>
        {
            var stored = await db.Users.IgnoreQueryFilters().SingleAsync(item => item.UserId == user.UserId);
            stored.BranchId = _factory.BranchA1Id;
            await db.SaveChangesAsync();
            return 0;
        });

        var response = await client.GetAsync($"/api/Employees/{_factory.EmployeeA2Id}");
        Assert.True(
            response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound,
            $"Salon-wide token still read another branch: {(int)response.StatusCode}");
    }

    [Fact]
    public async Task User_manager_cannot_grant_themselves_the_admin_role()
    {
        var username = await AddIsolatedAdminAsync("clerk.admin");
        using var admin = await _factory.CreateAuthenticatedClientAsync(username);
        var adminRole = await RoleIdAsync(admin, "ADMIN");
        var usersManage = await PermissionIdAsync(admin, "users.manage");
        var createdRole = await admin.PostAsJsonAsync("/api/Roles", new { roleCode = "CLERK", roleName = "Clerk", description = "User clerk", isActive = true });
        var clerkRole = await createdRole.Content.ReadFromJsonAsync<RoleItem>();
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/Roles/{clerkRole!.RoleId}/permissions", new { permissionIds = new[] { usersManage } })).StatusCode);
        var createdUser = await admin.PostAsJsonAsync("/api/Users", UserBody("user.clerk", "General", clerkRole.RoleId));
        var clerk = await createdUser.Content.ReadFromJsonAsync<UserItem>();
        Assert.Equal(HttpStatusCode.OK, createdUser.StatusCode);

        using var clerkClient = await _factory.CreateAuthenticatedClientAsync("user.clerk");
        var escalated = await clerkClient.PutAsJsonAsync($"/api/Users/{clerk!.UserId}/roles", new { roleIds = new[] { adminRole } });
        var body = await escalated.Content.ReadAsStringAsync();
        Assert.True(
            escalated.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.BadRequest,
            $"User manager granted ADMIN: {(int)escalated.StatusCode} {body}");
    }

    [Fact]
    public async Task Role_manager_cannot_grant_themselves_a_permission_they_do_not_hold()
    {
        var username = await AddIsolatedAdminAsync("editor.admin");
        using var admin = await _factory.CreateAuthenticatedClientAsync(username);
        var rolesManage = await PermissionIdAsync(admin, "roles.manage");
        var usersManage = await PermissionIdAsync(admin, "users.manage");
        var createdRole = await admin.PostAsJsonAsync("/api/Roles", new { roleCode = "EDITOR", roleName = "Editor", description = "Role editor", isActive = true });
        var editorRole = await createdRole.Content.ReadFromJsonAsync<RoleItem>();
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/Roles/{editorRole!.RoleId}/permissions", new { permissionIds = new[] { rolesManage } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/Users", UserBody("role.editor", "General", editorRole.RoleId))).StatusCode);

        using var editor = await _factory.CreateAuthenticatedClientAsync("role.editor");
        var escalated = await editor.PutAsJsonAsync($"/api/Roles/{editorRole.RoleId}/permissions", new { permissionIds = new[] { rolesManage, usersManage } });
        var body = await escalated.Content.ReadAsStringAsync();
        Assert.True(
            escalated.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.BadRequest,
            $"Role manager granted users.manage: {(int)escalated.StatusCode} {body}");
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.GetAsync("/api/Users")).StatusCode);
    }

    private HttpClient ClientWith(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static string Resign(string accessToken, IReadOnlyDictionary<string, string?> claimUpdates, string? signingKey = null)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(accessToken);
        var claims = jwt.Claims
            .Where(claim => claim.Type is not ("exp" or "nbf" or "iat" or "iss" or "aud"))
            .Where(claim => !claimUpdates.ContainsKey(claim.Type))
            .Select(claim => new Claim(claim.Type, claim.Value))
            .ToList();
        claims.AddRange(claimUpdates.Where(update => update.Value != null).Select(update => new Claim(update.Key, update.Value!)));

        var token = new JwtSecurityToken(
            issuer: "SalonBooking",
            audience: "SalonBookingClient",
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-2),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? TestSigningKey)),
                SecurityAlgorithms.HmacSha256));
        return handler.WriteToken(token);
    }

    private static string FlipPayload(string accessToken)
    {
        var parts = accessToken.Split('.');
        var payload = parts[1].ToCharArray();
        payload[0] = payload[0] == 'a' ? 'b' : 'a';
        parts[1] = new string(payload);
        return string.Join('.', parts);
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
                FirstName = "Link",
                LastName = "Employee",
                MobileNo = "0711111111",
                Designation = "Stylist",
                HireDate = new DateTime(2020, 1, 1),
                Salary = 1000
            };
            db.Employees.Add(employee);
            await db.SaveChangesAsync();
            return employee.EmployeeId;
        });
    }

    private async Task<long> AddCustomerAsync(string code)
    {
        return await _factory.ReadAsync(async db =>
        {
            var customer = new Customer
            {
                TenantId = _factory.TenantAId,
                BranchId = _factory.BranchA1Id,
                CustomerCode = code,
                FirstName = "Link",
                LastName = "Customer",
                MobileNo = "0771111111"
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            return customer.CustomerId;
        });
    }

    private async Task<string> AddIsolatedAdminAsync(string username)
    {
        var hasher = new PasswordHasher<User>();
        await _factory.ReadAsync(async db =>
        {
            var tenant = new Tenant
            {
                TenantCode = "TEN-" + username.Replace(".", "", StringComparison.Ordinal),
                TenantName = username,
                BusinessName = username,
                SubscriptionPlan = SubscriptionPlan.Trial,
                SubscriptionStartDate = DateTime.UtcNow,
                SubscriptionEndDate = DateTime.UtcNow.AddYears(1),
                MaxBranches = 5,
                MaxUsers = 20
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            var user = new User
            {
                TenantId = tenant.TenantId,
                Username = username,
                FirstName = "Sole",
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
        return username;
    }

    private async Task SetPermissionActiveAsync(string code, bool isActive)
    {
        await _factory.ReadAsync(async db =>
        {
            var permission = await db.Permissions.IgnoreQueryFilters().SingleAsync(item => item.PermissionCode == code && !item.IsDeleted);
            permission.IsActive = isActive;
            await db.SaveChangesAsync();
            return 0;
        });
    }

    private static async Task<long> RoleIdAsync(HttpClient client, string code)
    {
        var roles = await client.GetFromJsonAsync<List<RoleItem>>("/api/Roles");
        return roles!.Single(role => role.RoleCode == code).RoleId;
    }

    private static async Task<long> PermissionIdAsync(HttpClient client, string code)
    {
        var permissions = await client.GetFromJsonAsync<List<PermissionItem>>("/api/Permissions");
        return permissions!.Single(item => item.PermissionCode == code).PermissionId;
    }

    private static object UserBody(
        string username,
        string category,
        long roleId,
        long? employeeId = null,
        long? customerId = null,
        long? branchId = null,
        string? password = null,
        string firstName = "Test")
    {
        return new
        {
            username,
            password = password ?? "Password1!",
            firstName,
            lastName = "User",
            userCategory = category,
            employeeId,
            customerId,
            branchId,
            roleIds = new[] { roleId }
        };
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

    private sealed class PermissionItem
    {
        public long PermissionId { get; set; }
        public string PermissionCode { get; set; } = string.Empty;
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
