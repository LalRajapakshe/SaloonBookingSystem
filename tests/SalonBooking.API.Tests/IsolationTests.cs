using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

namespace SalonBooking.API.Tests;

[Collection(ApiCollection.Name)]
public class IsolationTests
{
    private readonly ApiFactory _factory;

    public IsolationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Tenant_A_cannot_read_update_or_delete_tenant_B_records()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/Customers/{_factory.CustomerB1Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/Employees/{_factory.EmployeeB1Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/Services/{_factory.ServiceB1Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/Branch/{_factory.BranchB1Id}")).StatusCode);

        var customerUpdate = await client.PutAsJsonAsync(
            $"/api/Customers/{_factory.CustomerB1Id}",
            CustomerBody("Hacked", "0770000003", _factory.BranchB1Id));
        var employeeUpdate = await client.PutAsJsonAsync(
            $"/api/Employees/{_factory.EmployeeB1Id}",
            EmployeeBody("Hacked", "0710000003", _factory.BranchB1Id));
        var serviceUpdate = await client.PutAsJsonAsync(
            $"/api/Services/{_factory.ServiceB1Id}",
            ServiceBody("Hacked Cut", _factory.BranchB1Id, _factory.CategoryBId));
        var branchUpdate = await client.PutAsJsonAsync(
            $"/api/Branch/{_factory.BranchB1Id}",
            BranchBody(_factory.TenantBId, "Hacked Branch"));

        Assert.Equal(HttpStatusCode.NotFound, customerUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, employeeUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, serviceUpdate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, branchUpdate.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/Customers/{_factory.CustomerB1Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/Employees/{_factory.EmployeeB1Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/Services/{_factory.ServiceB1Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/Branch/{_factory.BranchB1Id}")).StatusCode);

        var customer = await _factory.ReadAsync(db => db.Customers.IgnoreQueryFilters().SingleAsync(item => item.CustomerId == _factory.CustomerB1Id));
        var employee = await _factory.ReadAsync(db => db.Employees.IgnoreQueryFilters().SingleAsync(item => item.EmployeeId == _factory.EmployeeB1Id));
        var service = await _factory.ReadAsync(db => db.Services.IgnoreQueryFilters().SingleAsync(item => item.ServiceId == _factory.ServiceB1Id));
        var branch = await _factory.ReadAsync(db => db.Branches.IgnoreQueryFilters().SingleAsync(item => item.BranchId == _factory.BranchB1Id));

        Assert.Equal("Saman", customer.FirstName);
        Assert.False(customer.IsDeleted);
        Assert.Equal("Saman", employee.FirstName);
        Assert.False(employee.IsDeleted);
        Assert.Equal("Cut B1", service.ServiceName);
        Assert.False(service.IsDeleted);
        Assert.Equal("Branch B1", branch.BranchName);
        Assert.True(branch.IsActive);
    }

    [Fact]
    public async Task Tenant_A_cannot_create_records_under_tenant_B()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);

        var response = await client.PostAsJsonAsync(
            "/api/Customers",
            CustomerBody("Foreign", "0770000099", _factory.BranchB1Id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(await _factory.ReadAsync(db => db.Customers.IgnoreQueryFilters().AnyAsync(item => item.MobileNo == "0770000099")));
    }

    [Fact]
    public async Task Tenant_wide_user_can_read_both_own_branches_and_not_tenant_B()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);

        var branchA1 = await client.GetFromJsonAsync<BranchItem>($"/api/Branch/{_factory.BranchA1Id}");
        var branchA2 = await client.GetFromJsonAsync<BranchItem>($"/api/Branch/{_factory.BranchA2Id}");
        var customerA1 = await client.GetFromJsonAsync<CustomerItem>($"/api/Customers/{_factory.CustomerA1Id}");
        var customerA2 = await client.GetFromJsonAsync<CustomerItem>($"/api/Customers/{_factory.CustomerA2Id}");
        var employeeA1 = await client.GetFromJsonAsync<IdItem>($"/api/Employees/{_factory.EmployeeA1Id}");
        var employeeA2 = await client.GetFromJsonAsync<IdItem>($"/api/Employees/{_factory.EmployeeA2Id}");
        var serviceA1 = await client.GetFromJsonAsync<IdItem>($"/api/Services/{_factory.ServiceA1Id}");
        var serviceA2 = await client.GetFromJsonAsync<IdItem>($"/api/Services/{_factory.ServiceA2Id}");

        Assert.Equal(_factory.TenantAId, branchA1!.TenantId);
        Assert.Equal(_factory.BranchA1Id, branchA1.BranchId);
        Assert.Equal(_factory.BranchA2Id, branchA2!.BranchId);
        Assert.Equal(_factory.BranchA1Id, customerA1!.BranchId);
        Assert.Equal(_factory.BranchA2Id, customerA2!.BranchId);
        Assert.Equal(_factory.EmployeeA1Id, employeeA1!.EmployeeId);
        Assert.Equal(_factory.EmployeeA2Id, employeeA2!.EmployeeId);
        Assert.Equal(_factory.ServiceA1Id, serviceA1!.ServiceId);
        Assert.Equal(_factory.ServiceA2Id, serviceA2!.ServiceId);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/Customers/{_factory.CustomerB1Id}")).StatusCode);
    }

    [Fact]
    public async Task Branch_user_can_read_own_branch_and_is_forbidden_from_the_other_branch()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync(ApiFactory.BranchUserAName);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/Customers/{_factory.CustomerA1Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/Employees/{_factory.EmployeeA1Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/Services/{_factory.ServiceA1Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/Branch/{_factory.BranchA1Id}")).StatusCode);

        await AssertForbidden(await client.GetAsync($"/api/Customers/{_factory.CustomerA2Id}"));
        await AssertForbidden(await client.GetAsync($"/api/Employees/{_factory.EmployeeA2Id}"));
        await AssertForbidden(await client.GetAsync($"/api/Services/{_factory.ServiceA2Id}"));
        await AssertForbidden(await client.GetAsync($"/api/Branch/{_factory.BranchA2Id}"));

        var customers = await client.GetFromJsonAsync<Page<CustomerItem>>("/api/Customers");
        Assert.Contains(customers!.Items, item => item.CustomerId == _factory.CustomerA1Id);
        Assert.DoesNotContain(customers.Items, item => item.CustomerId == _factory.CustomerA2Id);
        Assert.All(customers.Items, item => Assert.Equal(_factory.BranchA1Id, item.BranchId));
    }

    [Fact]
    public async Task Branch_user_cannot_create_update_or_delete_another_branch()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync(ApiFactory.BranchUserAName);

        await AssertForbidden(await client.PostAsJsonAsync(
            "/api/Customers",
            CustomerBody("Blocked", "0770000091", _factory.BranchA2Id)));
        await AssertForbidden(await client.PostAsJsonAsync(
            "/api/Employees",
            EmployeeBody("Blocked", "0710000091", _factory.BranchA2Id)));
        await AssertForbidden(await client.PostAsJsonAsync(
            "/api/Services",
            ServiceBody("Blocked Cut", _factory.BranchA2Id, _factory.CategoryAId)));
        await AssertForbidden(await client.PostAsJsonAsync(
            "/api/Branch",
            BranchBody(_factory.TenantAId, "Blocked Branch")));

        await AssertForbidden(await client.PutAsJsonAsync(
            $"/api/Customers/{_factory.CustomerA2Id}",
            CustomerBody("Hacked", "0770000002", _factory.BranchA2Id)));
        await AssertForbidden(await client.PutAsJsonAsync(
            $"/api/Employees/{_factory.EmployeeA2Id}",
            EmployeeBody("Hacked", "0710000002", _factory.BranchA2Id)));
        await AssertForbidden(await client.PutAsJsonAsync(
            $"/api/Services/{_factory.ServiceA2Id}",
            ServiceBody("Hacked Cut", _factory.BranchA2Id, _factory.CategoryAId)));
        await AssertForbidden(await client.PutAsJsonAsync(
            $"/api/Branch/{_factory.BranchA2Id}",
            BranchBody(_factory.TenantAId, "Hacked Branch")));

        await AssertForbidden(await client.DeleteAsync($"/api/Customers/{_factory.CustomerA2Id}"));
        await AssertForbidden(await client.DeleteAsync($"/api/Employees/{_factory.EmployeeA2Id}"));
        await AssertForbidden(await client.DeleteAsync($"/api/Services/{_factory.ServiceA2Id}"));
        await AssertForbidden(await client.DeleteAsync($"/api/Branch/{_factory.BranchA2Id}"));

        var customer = await _factory.ReadAsync(db => db.Customers.IgnoreQueryFilters().SingleAsync(item => item.CustomerId == _factory.CustomerA2Id));
        var employee = await _factory.ReadAsync(db => db.Employees.IgnoreQueryFilters().SingleAsync(item => item.EmployeeId == _factory.EmployeeA2Id));
        var service = await _factory.ReadAsync(db => db.Services.IgnoreQueryFilters().SingleAsync(item => item.ServiceId == _factory.ServiceA2Id));
        var branch = await _factory.ReadAsync(db => db.Branches.IgnoreQueryFilters().SingleAsync(item => item.BranchId == _factory.BranchA2Id));

        Assert.Equal("Kamal", customer.FirstName);
        Assert.Equal(_factory.BranchA2Id, customer.BranchId);
        Assert.False(customer.IsDeleted);
        Assert.Equal("Kamal", employee.FirstName);
        Assert.False(employee.IsDeleted);
        Assert.Equal("Cut A2", service.ServiceName);
        Assert.False(service.IsDeleted);
        Assert.Equal("Branch A2", branch.BranchName);
        Assert.True(branch.IsActive);
        Assert.False(await _factory.ReadAsync(db => db.Customers.IgnoreQueryFilters().AnyAsync(item => item.MobileNo == "0770000091")));
    }

    [Fact]
    public async Task Branch_user_cannot_move_an_own_record_to_another_branch()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync(ApiFactory.BranchUserAName);
        var created = await client.PostAsJsonAsync(
            "/api/Customers",
            CustomerBody("Owned", "0770000088", _factory.BranchA1Id));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var customer = await created.Content.ReadFromJsonAsync<CustomerItem>();

        await AssertForbidden(await client.PutAsJsonAsync(
            $"/api/Customers/{customer!.CustomerId}",
            CustomerBody("Moved", "0770000088", _factory.BranchA2Id)));

        var saved = await _factory.ReadAsync(db => db.Customers.IgnoreQueryFilters().SingleAsync(item => item.CustomerId == customer.CustomerId));
        Assert.Equal("Owned", saved.FirstName);
        Assert.Equal(_factory.BranchA1Id, saved.BranchId);
        Assert.Equal(_factory.TenantAId, saved.TenantId);
    }

    [Fact]
    public async Task Client_supplied_tenant_id_cannot_override_the_caller()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        var created = await client.PostAsJsonAsync(
            "/api/Branch",
            BranchBody(_factory.TenantBId, "Created For A"));

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var branch = await created.Content.ReadFromJsonAsync<BranchItem>();
        Assert.Equal(_factory.TenantAId, branch!.TenantId);
        Assert.True(branch.IsActive);

        var updated = await client.PutAsJsonAsync(
            $"/api/Branch/{branch.BranchId}",
            BranchBody(_factory.TenantBId, "Renamed For A"));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var saved = await _factory.ReadAsync(db => db.Branches.IgnoreQueryFilters().SingleAsync(item => item.BranchId == branch.BranchId));
        Assert.Equal(_factory.TenantAId, saved.TenantId);
        Assert.Equal("Renamed For A", saved.BranchName);
    }

    [Fact]
    public async Task Normal_tenant_users_cannot_create_a_tenant()
    {
        var before = await _factory.ReadAsync(db => db.Tenants.IgnoreQueryFilters().CountAsync());

        using var admin = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        using var branchUser = await _factory.CreateAuthenticatedClientAsync(ApiFactory.BranchUserAName);

        await AssertForbidden(await admin.PostAsJsonAsync("/api/Tenant", TenantBody("New Tenant")));
        await AssertForbidden(await branchUser.PostAsJsonAsync("/api/Tenant", TenantBody("Another Tenant")));

        var after = await _factory.ReadAsync(db => db.Tenants.IgnoreQueryFilters().CountAsync());
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Branch_response_returns_the_stored_active_flag()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);

        var active = await client.GetFromJsonAsync<BranchItem>($"/api/Branch/{_factory.BranchA1Id}");
        var inactive = await client.GetFromJsonAsync<BranchItem>($"/api/Branch/{_factory.InactiveBranchId}");

        Assert.True(active!.IsActive);
        Assert.False(inactive!.IsActive);
    }

    private static async Task AssertForbidden(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Exception", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("another branch", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" at ", body, StringComparison.Ordinal);
    }

    private static object CustomerBody(string firstName, string mobile, long branchId)
    {
        return new
        {
            firstName,
            lastName = "Customer",
            mobileNo = mobile,
            dateOfBirth = "1990-01-01",
            branchId,
            isActive = true
        };
    }

    private static object EmployeeBody(string firstName, string mobile, long branchId)
    {
        return new
        {
            firstName,
            lastName = "Employee",
            mobileNo = mobile,
            designation = "Stylist",
            hireDate = "2020-01-01",
            salary = 1000,
            branchId,
            isActive = true
        };
    }

    private static object ServiceBody(string name, long branchId, long categoryId)
    {
        return new
        {
            branchId,
            serviceCategoryId = categoryId,
            serviceName = name,
            description = "Test",
            durationMinutes = 30,
            price = 100,
            cost = 10,
            commissionPercentage = 5,
            isActive = true
        };
    }

    private static object BranchBody(long tenantId, string branchName)
    {
        return new
        {
            tenantId,
            branchName,
            isHeadOffice = false,
            isActive = true
        };
    }

    private static object TenantBody(string name)
    {
        return new
        {
            tenantName = name,
            businessName = name,
            maxBranches = 2,
            maxUsers = 5
        };
    }

    private sealed class Page<T>
    {
        public List<T> Items { get; set; } = new();
    }

    private sealed class CustomerItem
    {
        public long CustomerId { get; set; }
        public long BranchId { get; set; }
        public long TenantId { get; set; }
    }

    private sealed class BranchItem
    {
        public long BranchId { get; set; }
        public long TenantId { get; set; }
        public bool IsActive { get; set; }
    }

    private sealed class IdItem
    {
        public long EmployeeId { get; set; }
        public long ServiceId { get; set; }
    }
}
