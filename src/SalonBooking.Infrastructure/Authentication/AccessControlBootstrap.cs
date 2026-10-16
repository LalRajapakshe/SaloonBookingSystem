using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SalonBooking.Application.Security;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Persistence.Context;

namespace SalonBooking.Infrastructure.Authentication;

public static class AccessControlBootstrap
{
    public static async Task EnsureAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AccessControl");
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var context = scope.ServiceProvider.GetRequiredService<SalonBookingDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        if (!await AccessSchemaReadyAsync(context, cancellationToken))
        {
            logger.LogInformation(
                "Access-control storage is not ready. Apply the user access migration before provisioning accounts.");
            return;
        }

        await EnsureCatalogAsync(context, cancellationToken);

        var username = First(configuration["InitialAdmin:Username"], configuration["SeedAdmin:Username"]);
        var password = First(configuration["InitialAdmin:Password"], configuration["SeedAdmin:Password"]);
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("Initial admin provisioning skipped because username or password is not configured.");
            return;
        }

        await ProvisionInitialAdminAsync(
            context,
            hasher,
            username.Trim(),
            password,
            configuration["InitialAdmin:TenantCode"],
            logger,
            cancellationToken);
    }

    public static async Task EnsureCatalogAsync(SalonBookingDbContext context, CancellationToken cancellationToken = default)
    {
        var existing = await context.Permissions
            .IgnoreQueryFilters()
            .Where(permission => !permission.IsDeleted)
            .Select(permission => permission.PermissionCode)
            .ToListAsync(cancellationToken);
        var known = existing.ToHashSet(StringComparer.Ordinal);

        foreach (var definition in PermissionCatalog.All)
        {
            if (known.Contains(definition.Code))
            {
                continue;
            }

            context.Permissions.Add(new Permission
            {
                PermissionCode = definition.Code,
                PermissionName = definition.Name,
                Description = definition.Description,
                IsActive = true
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public static async Task EnsureTenantRolesAsync(
        SalonBookingDbContext context,
        long tenantId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCatalogAsync(context, cancellationToken);
        var permissions = await context.Permissions
            .Where(permission => permission.IsActive && !permission.IsDeleted)
            .ToListAsync(cancellationToken);

        await EnsureRoleAsync(context, tenantId, PermissionCatalog.AdminRoleCode, "Admin", "Tenant administrator", PermissionCatalog.StaffCodes, permissions, fillMissing: true, cancellationToken);
        await EnsureRoleAsync(context, tenantId, PermissionCatalog.StaffRoleCode, "Staff", "Salon staff", PermissionCatalog.OperationalCodes, permissions, fillMissing: false, cancellationToken);
        await EnsureRoleAsync(context, tenantId, PermissionCatalog.CustomerRoleCode, "Customer", "Customer account", PermissionCatalog.CustomerCodes, permissions, fillMissing: false, cancellationToken);
    }

    public static async Task AssignLegacyRoleAsync(
        SalonBookingDbContext context,
        User user,
        CancellationToken cancellationToken = default)
    {
        await EnsureTenantRolesAsync(context, user.TenantId, cancellationToken);
        var code = string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase)
            ? PermissionCatalog.AdminRoleCode
            : PermissionCatalog.StaffRoleCode;
        var role = await context.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(
            item => item.TenantId == user.TenantId &&
                    item.RoleCode == code &&
                    item.IsActive &&
                    !item.IsDeleted,
            cancellationToken);
        if (role == null)
        {
            throw new InvalidOperationException(
                $"The built-in {code} role is inactive or missing. Reactivate it manually or mark the inactive row deleted. Provisioning will not reactivate it or assign this user to it.");
        }

        var linked = await context.UserRoles.AnyAsync(
            item => item.UserId == user.UserId && item.RoleId == role.RoleId,
            cancellationToken);
        if (!linked)
        {
            context.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = role.RoleId });
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public static async Task ProvisionInitialAdminAsync(
        SalonBookingDbContext context,
        IPasswordHasher<User> passwordHasher,
        string username,
        string password,
        string? tenantCode,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await EnsureCatalogAsync(context, cancellationToken);
        var matches = context.Users.IgnoreQueryFilters().Where(user => user.Username == username && !user.IsDeleted);
        if (!string.IsNullOrWhiteSpace(tenantCode))
        {
            var code = tenantCode.Trim();
            matches = matches.Where(user =>
                context.Tenants.IgnoreQueryFilters().Any(tenant =>
                    tenant.TenantId == user.TenantId &&
                    tenant.TenantCode == code &&
                    !tenant.IsDeleted));
        }

        var users = await matches.Take(2).ToListAsync(cancellationToken);
        if (users.Count > 1)
        {
            logger.LogInformation("Initial admin provisioning skipped because the username matches more than one salon.");
            return;
        }

        if (users.Count == 1)
        {
            var existing = users[0];
            await EnsureTenantRolesAsync(context, existing.TenantId, cancellationToken);
            await LinkAdminRoleAsync(context, existing, logger, cancellationToken);
            logger.LogInformation("Initial admin account already exists. The password was left unchanged.");
            return;
        }

        var tenant = await ResolveTenantAsync(context, tenantCode, cancellationToken);
        await EnsureTenantRolesAsync(context, tenant.TenantId, cancellationToken);
        var admin = new User
        {
            TenantId = tenant.TenantId,
            BranchId = null,
            UserCategory = UserCategory.General,
            Username = username,
            FirstName = "System",
            LastName = "Administrator",
            Role = "Admin",
            IsActive = true
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, password);
        context.Users.Add(admin);
        await context.SaveChangesAsync(cancellationToken);
        await LinkAdminRoleAsync(context, admin, logger, cancellationToken);
        logger.LogInformation("Initial admin account was created.");
    }

    private static async Task EnsureRoleAsync(
        SalonBookingDbContext context,
        long tenantId,
        string code,
        string name,
        string description,
        IReadOnlySet<string> permissionCodes,
        IReadOnlyList<Permission> permissions,
        bool fillMissing,
        CancellationToken cancellationToken)
    {
        var role = await context.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(
            item => item.TenantId == tenantId && item.RoleCode == code && !item.IsDeleted,
            cancellationToken);
        if (role is { IsActive: false })
        {
            return;
        }

        if (role == null)
        {
            role = new Role
            {
                TenantId = tenantId,
                RoleCode = code,
                RoleName = name,
                Description = description,
                IsActive = true
            };
            context.Roles.Add(role);
            await context.SaveChangesAsync(cancellationToken);
        }

        var linked = await context.RolePermissions
            .Where(item => item.RoleId == role.RoleId)
            .Select(item => item.PermissionId)
            .ToListAsync(cancellationToken);
        if (!fillMissing && linked.Count > 0)
        {
            return;
        }

        var wanted = permissions.Where(permission => permissionCodes.Contains(permission.PermissionCode)).Select(permission => permission.PermissionId).ToHashSet();
        foreach (var permissionId in wanted.Except(linked))
        {
            context.RolePermissions.Add(new RolePermission
            {
                RoleId = role.RoleId,
                PermissionId = permissionId
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task LinkAdminRoleAsync(
        SalonBookingDbContext context,
        User user,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var role = await context.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(
            item => item.TenantId == user.TenantId &&
                    item.RoleCode == PermissionCatalog.AdminRoleCode &&
                    item.IsActive &&
                    !item.IsDeleted,
            cancellationToken);
        if (role == null)
        {
            logger.LogInformation(
                "Initial admin role link was skipped because the ADMIN role is inactive or missing. The password was left unchanged.");
            return;
        }

        var linked = await context.UserRoles.AnyAsync(
            item => item.UserId == user.UserId && item.RoleId == role.RoleId,
            cancellationToken);
        if (!linked)
        {
            context.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = role.RoleId });
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<Tenant> ResolveTenantAsync(
        SalonBookingDbContext context,
        string? tenantCode,
        CancellationToken cancellationToken)
    {
        Tenant? tenant = null;
        if (!string.IsNullOrWhiteSpace(tenantCode))
        {
            var code = tenantCode.Trim();
            tenant = await context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(
                item => item.TenantCode == code && !item.IsDeleted,
                cancellationToken);
        }

        tenant ??= await context.Tenants.IgnoreQueryFilters()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.TenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (tenant != null)
        {
            return tenant;
        }

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
        await context.SaveChangesAsync(cancellationToken);

        context.Branches.Add(new Branch
        {
            TenantId = tenant.TenantId,
            BranchCode = "BRN000001",
            BranchName = "Head Office",
            IsHeadOffice = true
        });
        await context.SaveChangesAsync(cancellationToken);
        return tenant;
    }

    private static async Task<bool> AccessSchemaReadyAsync(
        SalonBookingDbContext context,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COL_LENGTH(N'Users', N'UserCategory')";
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result != null && result != DBNull.Value;
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string? First(string? primary, string? fallback)
    {
        return string.IsNullOrWhiteSpace(primary) ? fallback : primary;
    }
}
