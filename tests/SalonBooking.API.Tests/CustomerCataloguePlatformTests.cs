using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Infrastructure.Authentication;
using SalonBooking.Persistence.Context;

namespace SalonBooking.API.Tests;

[Collection(ApiCollection.Name)]
public class CustomerCataloguePlatformTests
{
    private readonly ApiFactory _factory;

    public CustomerCataloguePlatformTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Platform_administrator_can_create_a_tenant_and_a_branch_for_that_tenant()
    {
        var token = await PlatformTokenAsync();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createdTenant = await client.PostAsJsonAsync("/api/Tenant", new
        {
            tenantName = "North Salon",
            businessName = "North Salon",
            maxBranches = 3,
            maxUsers = 10,
            administratorUsername = "north.admin",
            administratorPassword = ApiFactory.Password,
            administratorFirstName = "North",
            administratorLastName = "Admin"
        });
        Assert.Equal(HttpStatusCode.OK, createdTenant.StatusCode);
        var tenant = await createdTenant.Content.ReadFromJsonAsync<TenantItem>();
        Assert.NotNull(tenant);
        Assert.NotEqual(_factory.TenantAId, tenant!.TenantId);

        var createdBranch = await client.PostAsJsonAsync("/api/Branch", new
        {
            tenantId = tenant.TenantId,
            branchName = "North Studio",
            city = "Colombo",
            highlight = "Colour studio",
            isCustomerVisible = true,
            isHeadOffice = true
        });
        Assert.Equal(HttpStatusCode.OK, createdBranch.StatusCode);
        var branch = await createdBranch.Content.ReadFromJsonAsync<BranchItem>();
        Assert.Equal(tenant.TenantId, branch!.TenantId);
        Assert.Equal("Colour studio", branch.Highlight);

        using var salonAdmin = await _factory.CreateAuthenticatedClientAsync("north.admin");
        var denied = await salonAdmin.PostAsJsonAsync("/api/Branch", new
        {
            tenantId = tenant.TenantId,
            branchName = "Second Studio",
            isHeadOffice = false
        });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Platform_administrator_can_list_every_tenant()
    {
        var token = await PlatformTokenAsync();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var listed = await client.GetAsync($"/api/Platform/tenants?tenantId={_factory.TenantBId}&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        var body = await listed.Content.ReadAsStringAsync();
        Assert.DoesNotContain("subscriptionPlan", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("businessRegistrationNo", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("logoUrl", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"address\"", body, StringComparison.OrdinalIgnoreCase);
        var page = JsonSerializer.Deserialize<TenantPage>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains(page!.Items, item => item.TenantId == _factory.TenantAId);
        Assert.Contains(page.Items, item => item.TenantId == _factory.TenantBId);

        var homeOnly = await client.GetFromJsonAsync<TenantPage>("/api/Tenant?pageSize=100");
        Assert.Contains(homeOnly!.Items, item => item.TenantId == _factory.TenantAId);
        Assert.DoesNotContain(homeOnly.Items, item => item.TenantId == _factory.TenantBId);
    }

    [Fact]
    public async Task Salon_administrator_staff_and_customers_cannot_list_every_tenant()
    {
        using var salon = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        Assert.Equal(HttpStatusCode.Forbidden, (await salon.GetAsync("/api/Platform/tenants")).StatusCode);
        var own = await salon.GetFromJsonAsync<TenantPage>("/api/Tenant?pageSize=100");
        Assert.Contains(own!.Items, item => item.TenantId == _factory.TenantAId);
        Assert.DoesNotContain(own.Items, item => item.TenantId == _factory.TenantBId);

        using var staff = await _factory.CreateAuthenticatedClientAsync(ApiFactory.BranchUserAName);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/Platform/tenants")).StatusCode);

        var customerToken = await CustomerTokenAsync();
        using var customer = _factory.CreateClient();
        customer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/Platform/tenants")).StatusCode);

        using var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/Platform/tenants")).StatusCode);
    }

    [Fact]
    public async Task The_only_salon_administrator_cannot_be_deactivated()
    {
        var token = await PlatformTokenAsync();
        using var platform = _factory.CreateClient();
        platform.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var created = await platform.PostAsJsonAsync("/api/Tenant", new
        {
            tenantName = "Solo Salon",
            businessName = "Solo Salon",
            maxBranches = 2,
            maxUsers = 5,
            administratorUsername = "solo.admin",
            administratorPassword = ApiFactory.Password,
            administratorFirstName = "Solo",
            administratorLastName = "Admin"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var adminId = await _factory.ReadAsync(db => db.Users.IgnoreQueryFilters().Where(item => item.Username == "solo.admin").Select(item => item.UserId).SingleAsync());

        using var salon = await _factory.CreateAuthenticatedClientAsync("solo.admin");
        var response = await salon.PostAsJsonAsync($"/api/Users/{adminId}/activation", new { isActive = false });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var stillActive = await _factory.ReadAsync(db => db.Users.IgnoreQueryFilters().SingleAsync(item => item.UserId == adminId));
        Assert.True(stillActive.IsActive);
    }

    [Fact]
    public async Task Customer_catalogue_hides_other_tenants_hr_fields_and_unpublished_rows()
    {
        await PublishCatalogueAsync();
        var token = await CustomerTokenAsync();
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/Employees")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/Branch")).StatusCode);

        var branches = await client.GetFromJsonAsync<List<CustomerBranch>>("/api/CustomerCatalogue/branches");
        Assert.Contains(branches!, item => item.BranchId == _factory.BranchA1Id);
        Assert.DoesNotContain(branches!, item => item.BranchId == _factory.BranchB1Id);
        Assert.DoesNotContain(branches!, item => item.BranchId == _factory.InactiveBranchId);

        var services = await client.GetFromJsonAsync<List<CustomerServiceItem>>(
            $"/api/CustomerCatalogue/branches/{_factory.BranchA1Id}/services");
        var service = Assert.Single(services!, item => item.ServiceId == _factory.ServiceA1Id);
        Assert.Equal("Cut A1", service.ServiceName);
        var raw = await client.GetStringAsync($"/api/CustomerCatalogue/branches/{_factory.BranchA1Id}/services");
        Assert.DoesNotContain("commissionPercentage", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cost", raw, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/CustomerCatalogue/branches/{_factory.BranchB1Id}/services")).StatusCode);

        var stylists = await client.GetFromJsonAsync<List<CustomerStylist>>(
            $"/api/CustomerCatalogue/branches/{_factory.BranchA1Id}/services/{_factory.ServiceA1Id}/stylists");
        var stylist = Assert.Single(stylists!);
        Assert.Equal(_factory.EmployeeA1Id, stylist.EmployeeId);
        Assert.Contains(stylist.Qualifications, item => item.IsVerified);
        Assert.DoesNotContain(stylist.Qualifications, item => item.QualificationName == "Hidden diploma");
        var stylistRaw = await client.GetStringAsync($"/api/CustomerCatalogue/stylists/{_factory.EmployeeA1Id}");
        Assert.DoesNotContain("salary", stylistRaw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mobileNo", stylistRaw, StringComparison.OrdinalIgnoreCase);

        var monday = new DateOnly(2026, 11, 9);
        var availability = await client.GetFromJsonAsync<AvailabilityBody>(
            $"/api/CustomerCatalogue/availability?branchId={_factory.BranchA1Id}&serviceId={_factory.ServiceA1Id}&employeeId={_factory.EmployeeA1Id}&date={monday:yyyy-MM-dd}");
        Assert.Contains(availability!.Slots, slot => slot.Available);
        Assert.Contains(availability.Slots, slot => !slot.Available);

        var booked = availability.Slots.First(slot => !slot.Available);
        var rejected = await client.PostAsJsonAsync("/api/CustomerAppointments", new
        {
            serviceId = _factory.ServiceA1Id,
            branchId = _factory.BranchA1Id,
            preferredEmployeeId = _factory.EmployeeA1Id,
            appointmentDate = monday,
            preferredStartTime = booked.StartTime
        });
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);

        var open = availability.Slots.First(slot => slot.Available);
        var accepted = await client.PostAsJsonAsync("/api/CustomerAppointments", new
        {
            serviceId = _factory.ServiceA1Id,
            branchId = _factory.BranchA1Id,
            preferredEmployeeId = _factory.EmployeeA1Id,
            appointmentDate = monday,
            preferredStartTime = open.StartTime
        });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var saved = await accepted.Content.ReadFromJsonAsync<AppointmentItem>();
        Assert.Equal("Pending", saved!.Status);
        Assert.Null(saved.Services[0].EmployeeId);
        Assert.Null(saved.Services[0].StartTime);
    }

    private async Task PublishCatalogueAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SalonBookingDbContext>();
        var branch = await db.Branches.IgnoreQueryFilters().SingleAsync(item => item.BranchId == _factory.BranchA1Id);
        var service = await db.Services.IgnoreQueryFilters().Include(item => item.ServiceCategory).SingleAsync(item => item.ServiceId == _factory.ServiceA1Id);
        var employee = await db.Employees.IgnoreQueryFilters().SingleAsync(item => item.EmployeeId == _factory.EmployeeA1Id);
        branch.IsCustomerVisible = true;
        branch.Highlight = "Flagship";
        service.IsCustomerVisible = true;
        service.Description = "A precise cut";
        service.IsPopular = true;
        employee.IsCustomerVisible = true;
        employee.Specialisation = "Cuts";
        employee.ProfessionalDescription = "Senior stylist";

        if (!await db.EmployeeServices.IgnoreQueryFilters().AnyAsync(item =>
                item.EmployeeId == employee.EmployeeId && item.ServiceId == service.ServiceId && !item.IsDeleted))
        {
            db.EmployeeServices.Add(new EmployeeService
            {
                TenantId = _factory.TenantAId,
                BranchId = _factory.BranchA1Id,
                EmployeeId = employee.EmployeeId,
                ServiceId = service.ServiceId,
                IsActive = true
            });
        }

        var monday = DayOfWeek.Monday;
        if (!await db.StaffSchedules.IgnoreQueryFilters().AnyAsync(item =>
                item.EmployeeId == employee.EmployeeId && item.DayOfWeek == (byte)monday && !item.IsDeleted))
        {
            db.StaffSchedules.Add(new StaffSchedule
            {
                TenantId = _factory.TenantAId,
                BranchId = _factory.BranchA1Id,
                EmployeeId = employee.EmployeeId,
                DayOfWeek = (byte)monday,
                SegmentType = ScheduleSegmentType.Working,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(12, 0)
            });
        }

        var date = new DateOnly(2026, 11, 9);
        if (!await db.AppointmentServices.IgnoreQueryFilters().AnyAsync(item =>
                item.EmployeeId == employee.EmployeeId && item.StartTime == date.ToDateTime(new TimeOnly(9, 0))))
        {
            var appointment = new Appointment
            {
                TenantId = _factory.TenantAId,
                BranchId = _factory.BranchA1Id,
                CustomerId = _factory.CustomerA1Id,
                AppointmentDate = date,
                Status = AppointmentStatus.Confirmed
            };
            db.Appointments.Add(appointment);
            db.AppointmentServices.Add(new AppointmentService
            {
                TenantId = _factory.TenantAId,
                BranchId = _factory.BranchA1Id,
                Appointment = appointment,
                ServiceId = service.ServiceId,
                EmployeeId = employee.EmployeeId,
                StartTime = date.ToDateTime(new TimeOnly(9, 0)),
                EndTime = date.ToDateTime(new TimeOnly(9, 30)),
                DurationMinutes = 30,
                UnitPrice = 100,
                Sequence = 1,
                RowVersion = [1]
            });
        }

        if (!await db.Qualifications.IgnoreQueryFilters().AnyAsync(item => item.TenantId == _factory.TenantAId && item.QualificationName == "NVQ Hair"))
        {
            var shown = new Qualification
            {
                TenantId = _factory.TenantAId,
                QualificationName = "NVQ Hair",
                QualificationLevel = "Level 3"
            };
            var hidden = new Qualification
            {
                TenantId = _factory.TenantAId,
                QualificationName = "Hidden diploma"
            };
            db.Qualifications.AddRange(shown, hidden);
            await db.SaveChangesAsync();
            db.EmployeeQualifications.Add(new EmployeeQualification
            {
                TenantId = _factory.TenantAId,
                EmployeeId = employee.EmployeeId,
                QualificationId = shown.QualificationId,
                AwardingInstitution = "City Guild",
                CompletionYear = 2020,
                VerificationStatus = QualificationVerificationStatus.Verified,
                IsCustomerVisible = true
            });
            db.EmployeeQualifications.Add(new EmployeeQualification
            {
                TenantId = _factory.TenantAId,
                EmployeeId = employee.EmployeeId,
                QualificationId = hidden.QualificationId,
                VerificationStatus = QualificationVerificationStatus.Pending,
                IsCustomerVisible = false
            });
        }

        await db.SaveChangesAsync();
    }

    private async Task<string> CustomerTokenAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SalonBookingDbContext>();
        var existing = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Username == "customer.catalogue");
        if (existing == null)
        {
            var hasher = new PasswordHasher<User>();
            var customer = new Customer
            {
                TenantId = _factory.TenantAId,
                BranchId = _factory.BranchA1Id,
                CustomerCode = "CUS-CATALOGUE",
                FirstName = "Nimal",
                LastName = "Customer",
                MobileNo = "0770000099"
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            var user = new User
            {
                TenantId = _factory.TenantAId,
                BranchId = _factory.BranchA1Id,
                UserCategory = UserCategory.Customer,
                CustomerId = customer.CustomerId,
                Username = "customer.catalogue",
                FirstName = "Nimal",
                LastName = "Customer",
                Role = "Customer"
            };
            user.PasswordHash = hasher.HashPassword(user, ApiFactory.Password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var login = await _factory.LoginAsync("customer.catalogue");
        return login.AccessToken;
    }

    private async Task<string> PlatformTokenAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SalonBookingDbContext>();
        var existing = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.Username == "platform.admin");
        if (existing == null)
        {
            var hasher = new PasswordHasher<User>();
            var user = new User
            {
                TenantId = _factory.TenantAId,
                UserCategory = UserCategory.General,
                Username = "platform.admin",
                FirstName = "Platform",
                LastName = "Admin",
                Role = "Platform",
                IsPlatformAdministrator = true
            };
            user.PasswordHash = hasher.HashPassword(user, ApiFactory.Password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            await AccessControlBootstrap.AssignLegacyRoleAsync(db, user);
        }

        var login = await _factory.LoginAsync("platform.admin");
        return login.AccessToken;
    }

    private sealed class TenantItem
    {
        public long TenantId { get; set; }
    }

    private sealed class TenantPage
    {
        public List<TenantItem> Items { get; set; } = [];
    }

    private sealed class BranchItem
    {
        public long TenantId { get; set; }
        public string? Highlight { get; set; }
    }

    private sealed class CustomerBranch
    {
        public long BranchId { get; set; }
    }

    private sealed class CustomerServiceItem
    {
        public long ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
    }

    private sealed class CustomerStylist
    {
        public long EmployeeId { get; set; }
        public List<CustomerQualification> Qualifications { get; set; } = [];
    }

    private sealed class CustomerQualification
    {
        public string QualificationName { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
    }

    private sealed class AvailabilityBody
    {
        public List<SlotItem> Slots { get; set; } = [];
    }

    private sealed class SlotItem
    {
        public DateTime StartTime { get; set; }
        public bool Available { get; set; }
    }

    private sealed class AppointmentItem
    {
        public string Status { get; set; } = string.Empty;
        public List<LineItem> Services { get; set; } = [];
    }

    private sealed class LineItem
    {
        public long? EmployeeId { get; set; }
        public DateTime? StartTime { get; set; }
    }
}
