using System.Net;
using System.Net.Http.Json;

namespace SalonBooking.API.Tests;

[Collection(ApiCollection.Name)]
public class SchedulingIsolationTests
{
    private readonly ApiFactory _factory;

    public SchedulingIsolationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Appointment_access_follows_tenant_and_branch()
    {
        using var tenantClient = await _factory.CreateAuthenticatedClientAsync(ApiFactory.TenantUserAName);
        var created = await tenantClient.PostAsJsonAsync("/api/Appointments", new
        {
            branchId = _factory.BranchA1Id,
            customerId = _factory.CustomerA1Id,
            appointmentDate = "2026-10-06",
            notes = "Isolation"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var appointment = await created.Content.ReadFromJsonAsync<AppointmentItem>();

        using var otherTenant = await _factory.CreateAuthenticatedClientAsync(ApiFactory.BranchUserBName);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await otherTenant.GetAsync($"/api/Appointments/{appointment!.AppointmentId}")).StatusCode);

        using var branchClient = await _factory.CreateAuthenticatedClientAsync(ApiFactory.BranchUserAName);
        Assert.Equal(
            HttpStatusCode.OK,
            (await branchClient.GetAsync($"/api/Appointments/{appointment.AppointmentId}")).StatusCode);

        var otherBranch = await tenantClient.PostAsJsonAsync("/api/Appointments", new
        {
            branchId = _factory.BranchA2Id,
            customerId = _factory.CustomerA2Id,
            appointmentDate = "2026-10-06"
        });
        Assert.Equal(HttpStatusCode.OK, otherBranch.StatusCode);
        var branchTwo = await otherBranch.Content.ReadFromJsonAsync<AppointmentItem>();

        var forbidden = await branchClient.GetAsync($"/api/Appointments/{branchTwo!.AppointmentId}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var body = await forbidden.Content.ReadAsStringAsync();
        Assert.DoesNotContain("another branch", body, StringComparison.OrdinalIgnoreCase);

        var blockedCreate = await branchClient.PostAsJsonAsync("/api/Appointments", new
        {
            branchId = _factory.BranchA2Id,
            customerId = _factory.CustomerA2Id,
            appointmentDate = "2026-10-07"
        });
        Assert.Equal(HttpStatusCode.Forbidden, blockedCreate.StatusCode);
    }

    private sealed class AppointmentItem
    {
        public long AppointmentId { get; set; }
    }
}
