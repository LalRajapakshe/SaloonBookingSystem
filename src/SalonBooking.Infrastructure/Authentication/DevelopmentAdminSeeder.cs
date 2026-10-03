using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Persistence.Context;

namespace SalonBooking.Infrastructure.Authentication;

public static class DevelopmentAdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var configuration = scope.ServiceProvider
            .GetRequiredService<IConfiguration>();
        var username = configuration["SeedAdmin:Username"];
        var password = configuration["SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var context = scope.ServiceProvider
            .GetRequiredService<SalonBookingDbContext>();
        var passwordHasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher<User>>();

        var existing = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(user => user.Username == username);

        if (existing != null)
        {
            if (string.IsNullOrWhiteSpace(existing.PasswordHash))
            {
                existing.PasswordHash = passwordHasher.HashPassword(
                    existing,
                    password);
                await context.SaveChangesAsync();
            }

            return;
        }

        var tenant = await context.Tenants
            .IgnoreQueryFilters()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.TenantId)
            .FirstOrDefaultAsync();

        if (tenant == null)
        {
            tenant = new Tenant
            {
                TenantCode = "TEN000001",
                TenantName = "Default Salon",
                BusinessName = "Default Salon",
                SubscriptionPlan = SubscriptionPlan.Trial,
                SubscriptionStartDate = DateTime.UtcNow,
                SubscriptionEndDate = DateTime.UtcNow.AddYears(1),
                MaxBranches = 5,
                MaxUsers = 20
            };
            context.Tenants.Add(tenant);
            await context.SaveChangesAsync();
        }

        var branch = await context.Branches
            .IgnoreQueryFilters()
            .Where(item =>
                item.TenantId == tenant.TenantId &&
                !item.IsDeleted)
            .OrderByDescending(item => item.IsHeadOffice)
            .ThenBy(item => item.BranchId)
            .FirstOrDefaultAsync();

        if (branch == null)
        {
            branch = new Branch
            {
                TenantId = tenant.TenantId,
                BranchCode = "BRN000001",
                BranchName = "Head Office",
                IsHeadOffice = true
            };
            context.Branches.Add(branch);
            await context.SaveChangesAsync();
        }

        var admin = new User
        {
            TenantId = tenant.TenantId,
            BranchId = null,
            Username = username,
            FirstName = "System",
            LastName = "Administrator",
            Role = "Admin"
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, password);
        context.Users.Add(admin);
        await context.SaveChangesAsync();
    }
}
