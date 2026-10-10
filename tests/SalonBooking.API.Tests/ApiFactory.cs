using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Persistence.Context;

namespace SalonBooking.API.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string Password = "Password1!";
    public const string BranchUserAName = "branch.a1";
    public const string TenantUserAName = "tenant.a";
    public const string BranchUserBName = "branch.b1";
    public const string InactiveUserName = "inactive.a";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private bool _seeded;

    public long TenantAId { get; private set; }
    public long TenantBId { get; private set; }
    public long BranchA1Id { get; private set; }
    public long BranchA2Id { get; private set; }
    public long InactiveBranchId { get; private set; }
    public long BranchB1Id { get; private set; }
    public long BranchUserAId { get; private set; }
    public long TenantUserAId { get; private set; }
    public long CategoryAId { get; private set; }
    public long CategoryBId { get; private set; }
    public long CustomerA1Id { get; private set; }
    public long CustomerA2Id { get; private set; }
    public long CustomerB1Id { get; private set; }
    public long EmployeeA1Id { get; private set; }
    public long EmployeeA2Id { get; private set; }
    public long EmployeeB1Id { get; private set; }
    public long ServiceA1Id { get; private set; }
    public long ServiceA2Id { get; private set; }
    public long ServiceB1Id { get; private set; }

    public ApiFactory()
    {
        _connection.Open();
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            "Server=localhost;Database=SalonBookingTests;Trusted_Connection=True;TrustServerCertificate=True;");
        Environment.SetEnvironmentVariable(
            "JwtSettings__SecretKey",
            "test-signing-key-0123456789-test-signing-key");
        Environment.SetEnvironmentVariable("JwtSettings__Issuer", "SalonBooking");
        Environment.SetEnvironmentVariable("JwtSettings__Audience", "SalonBookingClient");
        Environment.SetEnvironmentVariable("JwtSettings__ExpiryMinutes", "60");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=localhost;Database=SalonBookingTests;Trusted_Connection=True;TrustServerCertificate=True;",
                ["JwtSettings:SecretKey"] =
                    "test-signing-key-0123456789-test-signing-key",
                ["JwtSettings:Issuer"] = "SalonBooking",
                ["JwtSettings:Audience"] = "SalonBookingClient",
                ["JwtSettings:ExpiryMinutes"] = "60"
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptors = services.Where(service =>
                service.ServiceType == typeof(DbContextOptions<SalonBookingDbContext>) ||
                service.ServiceType == typeof(SalonBookingDbContext)).ToList();

            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<SalonBookingDbContext>(options =>
                options.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SalonBookingDbContext>();
        database.Database.EnsureCreated();
        SeedAsync(database).GetAwaiter().GetResult();
        return host;
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string username)
    {
        var client = CreateClient();
        var login = await LoginAsync(username);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return client;
    }

    public async Task<LoginBody> LoginAsync(
        string username,
        string? password = null)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/Auth/login",
            new { username, password = password ?? Password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginBody>();
        return body ?? throw new InvalidOperationException("Login returned an empty body.");
    }

    public async Task<T> ReadAsync<T>(Func<SalonBookingDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<SalonBookingDbContext>();
        return await query(database);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }

    private async Task SeedAsync(SalonBookingDbContext database)
    {
        if (_seeded)
        {
            return;
        }

        var hasher = new PasswordHasher<User>();
        var tenantA = NewTenant("TEN-A", "Tenant A");
        var tenantB = NewTenant("TEN-B", "Tenant B");
        database.Tenants.AddRange(tenantA, tenantB);
        await database.SaveChangesAsync();

        var branchA1 = NewBranch(tenantA.TenantId, "BRN-A1", "Branch A1", true);
        var branchA2 = NewBranch(tenantA.TenantId, "BRN-A2", "Branch A2", false);
        var inactiveBranch = NewBranch(tenantA.TenantId, "BRN-AIN", "Branch Inactive", false);
        var branchB1 = NewBranch(tenantB.TenantId, "BRN-B1", "Branch B1", true);
        database.Branches.AddRange(branchA1, branchA2, inactiveBranch, branchB1);
        await database.SaveChangesAsync();
        inactiveBranch.IsActive = false;
        await database.SaveChangesAsync();

        var branchUserA = NewUser(tenantA.TenantId, branchA1.BranchId, BranchUserAName, "Staff", hasher);
        var tenantUserA = NewUser(tenantA.TenantId, null, TenantUserAName, "Admin", hasher);
        var branchUserB = NewUser(tenantB.TenantId, branchB1.BranchId, BranchUserBName, "Staff", hasher);
        var inactiveUser = NewUser(tenantA.TenantId, branchA1.BranchId, InactiveUserName, "Staff", hasher);
        database.Users.AddRange(branchUserA, tenantUserA, branchUserB, inactiveUser);
        await database.SaveChangesAsync();
        inactiveUser.IsActive = false;
        await database.SaveChangesAsync();

        var categoryA = NewCategory(tenantA.TenantId, "CAT-A", "Hair A");
        var categoryB = NewCategory(tenantB.TenantId, "CAT-B", "Hair B");
        database.ServiceCategories.AddRange(categoryA, categoryB);
        await database.SaveChangesAsync();

        var customerA1 = NewCustomer(tenantA.TenantId, branchA1.BranchId, "CUS-A1", "Nimal", "0770000001");
        var customerA2 = NewCustomer(tenantA.TenantId, branchA2.BranchId, "CUS-A2", "Kamal", "0770000002");
        var customerB1 = NewCustomer(tenantB.TenantId, branchB1.BranchId, "CUS-B1", "Saman", "0770000003");
        database.Customers.AddRange(customerA1, customerA2, customerB1);

        var employeeA1 = NewEmployee(tenantA.TenantId, branchA1.BranchId, "EMP-A1", "Nimal", "0710000001");
        var employeeA2 = NewEmployee(tenantA.TenantId, branchA2.BranchId, "EMP-A2", "Kamal", "0710000002");
        var employeeB1 = NewEmployee(tenantB.TenantId, branchB1.BranchId, "EMP-B1", "Saman", "0710000003");
        database.Employees.AddRange(employeeA1, employeeA2, employeeB1);

        var serviceA1 = NewService(tenantA.TenantId, branchA1.BranchId, categoryA.ServiceCategoryId, "SER-A1", "Cut A1");
        var serviceA2 = NewService(tenantA.TenantId, branchA2.BranchId, categoryA.ServiceCategoryId, "SER-A2", "Cut A2");
        var serviceB1 = NewService(tenantB.TenantId, branchB1.BranchId, categoryB.ServiceCategoryId, "SER-B1", "Cut B1");
        database.Services.AddRange(serviceA1, serviceA2, serviceB1);
        await database.SaveChangesAsync();

        TenantAId = tenantA.TenantId;
        TenantBId = tenantB.TenantId;
        BranchA1Id = branchA1.BranchId;
        BranchA2Id = branchA2.BranchId;
        InactiveBranchId = inactiveBranch.BranchId;
        BranchB1Id = branchB1.BranchId;
        BranchUserAId = branchUserA.UserId;
        TenantUserAId = tenantUserA.UserId;
        CategoryAId = categoryA.ServiceCategoryId;
        CategoryBId = categoryB.ServiceCategoryId;
        CustomerA1Id = customerA1.CustomerId;
        CustomerA2Id = customerA2.CustomerId;
        CustomerB1Id = customerB1.CustomerId;
        EmployeeA1Id = employeeA1.EmployeeId;
        EmployeeA2Id = employeeA2.EmployeeId;
        EmployeeB1Id = employeeB1.EmployeeId;
        ServiceA1Id = serviceA1.ServiceId;
        ServiceA2Id = serviceA2.ServiceId;
        ServiceB1Id = serviceB1.ServiceId;
        _seeded = true;
    }

    private static Tenant NewTenant(string code, string name)
    {
        return new Tenant
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
    }

    private static Branch NewBranch(long tenantId, string code, string name, bool headOffice)
    {
        return new Branch
        {
            TenantId = tenantId,
            BranchCode = code,
            BranchName = name,
            IsHeadOffice = headOffice
        };
    }

    private static User NewUser(
        long tenantId,
        long? branchId,
        string username,
        string role,
        PasswordHasher<User> hasher)
    {
        var user = new User
        {
            TenantId = tenantId,
            BranchId = branchId,
            Username = username,
            FirstName = username,
            LastName = "User",
            Role = role
        };
        user.PasswordHash = hasher.HashPassword(user, Password);
        return user;
    }

    private static ServiceCategory NewCategory(long tenantId, string code, string name)
    {
        return new ServiceCategory
        {
            TenantId = tenantId,
            CategoryCode = code,
            CategoryName = name,
            DisplayOrder = 1
        };
    }

    private static Customer NewCustomer(
        long tenantId,
        long branchId,
        string code,
        string firstName,
        string mobile)
    {
        return new Customer
        {
            TenantId = tenantId,
            BranchId = branchId,
            CustomerCode = code,
            FirstName = firstName,
            LastName = "Customer",
            MobileNo = mobile
        };
    }

    private static Employee NewEmployee(
        long tenantId,
        long branchId,
        string code,
        string firstName,
        string mobile)
    {
        return new Employee
        {
            TenantId = tenantId,
            BranchId = branchId,
            EmployeeCode = code,
            FirstName = firstName,
            LastName = "Employee",
            MobileNo = mobile,
            Designation = "Stylist",
            HireDate = new DateTime(2020, 1, 1),
            Salary = 1000
        };
    }

    private static Service NewService(
        long tenantId,
        long branchId,
        long categoryId,
        string code,
        string name)
    {
        return new Service
        {
            TenantId = tenantId,
            BranchId = branchId,
            ServiceCategoryId = categoryId,
            ServiceCode = code,
            ServiceName = name,
            DurationMinutes = 30,
            Price = 100,
            Cost = 10,
            CommissionPercentage = 5
        };
    }
}

public sealed class LoginBody
{
    public string AccessToken { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public string Username { get; set; } = string.Empty;
}

[CollectionDefinition(ApiCollection.Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
