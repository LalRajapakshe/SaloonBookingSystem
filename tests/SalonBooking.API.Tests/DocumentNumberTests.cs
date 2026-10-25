using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalonBooking.Application.Documents;
using SalonBooking.Application.Features.Branch.DTOs;
using SalonBooking.Application.Features.Customer.DTOs;
using SalonBooking.Application.Features.Employee.DTOs;
using SalonBooking.Application.Features.Service.DTOs;
using SalonBooking.Application.Features.ServiceCategory.DTOs;
using SalonBooking.Application.Interfaces;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Infrastructure.Services;
using SalonBooking.Persistence.Context;

namespace SalonBooking.API.Tests;

public sealed class DocumentNumberTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly TestUser _user = new();
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserService>(_user);
        services.AddDbContext<SalonBookingDbContext>(options => options.UseSqlite(_connection));
        services.AddScoped<IDocumentNumberService, DocumentNumberService>();
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IServiceCategoryService, ServiceCategoryService>();
        services.AddScoped<IServiceService, ServiceService>();
        services.AddScoped<IEmployeeService, SalonBooking.Infrastructure.Services.EmployeeService>();
        services.AddScoped<ICustomerService, CustomerService>();
        _provider = services.BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<SalonBookingDbContext>();
        await database.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Each_master_takes_the_next_code_from_its_own_docs_row()
    {
        var tenantId = await AddTenantAsync();
        _user.TenantId = tenantId;

        await using var scope = _provider.CreateAsyncScope();
        var branches = scope.ServiceProvider.GetRequiredService<IBranchService>();
        var categories = scope.ServiceProvider.GetRequiredService<IServiceCategoryService>();
        var services = scope.ServiceProvider.GetRequiredService<IServiceService>();
        var employees = scope.ServiceProvider.GetRequiredService<IEmployeeService>();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerService>();

        var firstBranch = await branches.CreateAsync(BranchRequest("Colombo"));
        var secondBranch = await branches.CreateAsync(BranchRequest("Kandy"));
        var category = await categories.CreateAsync(new CreateServiceCategoryRequest
        {
            CategoryName = "Hair",
            Description = "Hair",
            DisplayOrder = 1
        });
        var service = await services.CreateAsync(new CreateServiceRequest
        {
            BranchId = (int)firstBranch.BranchId,
            ServiceCategoryId = (int)category.ServiceCategoryId,
            ServiceName = "Blowout",
            Description = "Style",
            DurationMinutes = 45,
            Price = 4500
        });
        var employee = await employees.CreateAsync(new CreateEmployeeRequest
        {
            BranchId = firstBranch.BranchId,
            FirstName = "Amara",
            LastName = "Jayawardena",
            MobileNo = "0770000001"
        });
        var firstCustomer = await customers.CreateAsync(new CreateCustomerRequest
        {
            BranchId = firstBranch.BranchId,
            FirstName = "Maya",
            LastName = "Fernando",
            MobileNo = "0770000002"
        });
        var secondCustomer = await customers.CreateAsync(new CreateCustomerRequest
        {
            BranchId = firstBranch.BranchId,
            FirstName = "Nia",
            LastName = "Perera",
            MobileNo = "0770000003"
        });

        Assert.Equal("BRN000001", firstBranch.BranchCode);
        Assert.Equal("BRN000002", secondBranch.BranchCode);
        Assert.Equal("CAT000001", category.CategoryCode);
        Assert.Equal("SER000001", service.ServiceCode);
        Assert.Equal("EMP000001", employee.EmployeeCode);
        Assert.Equal("CUS000001", firstCustomer.CustomerCode);
        Assert.Equal("CUS000002", secondCustomer.CustomerCode);
    }

    [Fact]
    public async Task A_failed_save_returns_the_number_to_the_series()
    {
        var tenantId = await AddTenantAsync();
        _user.TenantId = tenantId;

        await using var scope = _provider.CreateAsyncScope();
        var numbers = scope.ServiceProvider.GetRequiredService<IDocumentNumberService>();

        var failed = await Record.ExceptionAsync(() =>
            numbers.ExecuteAsync(async () =>
            {
                var code = await numbers.NextCodeAsync(tenantId, DocSeriesKind.Customer);
                if (code != "CUS000001")
                {
                    return code;
                }

                throw new InvalidOperationException("Save failed.");
            }));
        Assert.IsType<InvalidOperationException>(failed);
        Assert.Equal("Save failed.", failed.Message);

        var retry = await numbers.ExecuteAsync(() =>
            numbers.NextCodeAsync(tenantId, DocSeriesKind.Customer));

        Assert.Equal("CUS000001", retry);
    }

    [Fact]
    public async Task An_existing_record_continues_the_series_instead_of_starting_again()
    {
        var tenantId = await AddTenantAsync();
        _user.TenantId = tenantId;

        await using (var seed = _provider.CreateAsyncScope())
        {
            var database = seed.ServiceProvider.GetRequiredService<SalonBookingDbContext>();
            database.Branches.Add(new Branch
            {
                TenantId = tenantId,
                BranchCode = "BRN-OLD",
                BranchName = "Already there",
                IsHeadOffice = true
            });
            await database.SaveChangesAsync();
        }

        await using var scope = _provider.CreateAsyncScope();
        var branches = scope.ServiceProvider.GetRequiredService<IBranchService>();
        var created = await branches.CreateAsync(BranchRequest("Next studio"));

        Assert.Equal("BRN000002", created.BranchCode);
    }

    private async Task<long> AddTenantAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<SalonBookingDbContext>();
        var tenant = new Tenant
        {
            TenantCode = "TEN000001",
            TenantName = "Lumiere",
            BusinessName = "Lumiere",
            SubscriptionPlan = SubscriptionPlan.Trial,
            SubscriptionStartDate = DateTime.UtcNow,
            SubscriptionEndDate = DateTime.UtcNow.AddYears(1)
        };
        database.Tenants.Add(tenant);
        await database.SaveChangesAsync();
        return tenant.TenantId;
    }

    private static CreateBranchRequest BranchRequest(string name)
    {
        return new CreateBranchRequest
        {
            BranchName = name,
            City = name,
            IsHeadOffice = false
        };
    }

    private sealed class TestUser : ICurrentUserService
    {
        public bool IsAuthenticated => true;

        public long? UserId => 1;

        public long? TenantId { get; set; }

        public long? BranchId { get; set; }

        public string? Username => "tester";

        public string? Role => "Admin";

        public SalonBooking.Domain.Enums.UserCategory? UserCategory =>
            SalonBooking.Domain.Enums.UserCategory.General;

        public long? EmployeeId => null;

        public long? CustomerId => null;
    }
}
