using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Identity;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Security;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Infrastructure.Authentication;
using SalonBooking.Infrastructure.Security;
using SalonBooking.Persistence.Context;

namespace SalonBooking.Infrastructure.Services;

public sealed class UserAccountService : IUserAccountService
{
    private readonly SalonBookingDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher<User> _passwordHasher;

    public UserAccountService(
        SalonBookingDbContext context,
        ICurrentUserService currentUser,
        IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
    }

    public async Task<PagedResult<UserResponse>> GetUsersAsync(
        UserQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;
        var query = VisibleUsers().Where(user => user.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(user =>
                user.Username.Contains(search) ||
                user.FirstName.Contains(search) ||
                user.LastName.Contains(search) ||
                (user.Email != null && user.Email.Contains(search)) ||
                (user.MobileNo != null && user.MobileNo.Contains(search)));
        }

        var total = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderBy(user => user.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<UserResponse>
        {
            Items = users.Select(Map).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalRecords = total,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    public async Task<UserResponse> GetUserAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await RequireVisibleUserAsync(userId, cancellationToken);
        return Map(user);
    }

    public async Task<UserResponse> CreateAsync(SaveUserRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var username = RequiredUsername(request.Username);
        var password = RequiredPassword(request.Password);
        await EnsureUsernameAvailableAsync(tenantId, username, null, cancellationToken);
        var link = await ResolveLinkAsync(request, tenantId, null, cancellationToken);
        var roles = await LoadRolesAsync(tenantId, request.RoleIds, link.Category, cancellationToken);

        var user = new User
        {
            TenantId = tenantId,
            BranchId = link.BranchId,
            UserCategory = link.Category,
            EmployeeId = link.EmployeeId,
            CustomerId = link.CustomerId,
            Username = username,
            FirstName = RequiredName(request.FirstName, "First name"),
            LastName = RequiredName(request.LastName, "Last name"),
            Email = Clean(request.Email, 200),
            MobileNo = Clean(request.MobileNo, 20),
            Role = string.Empty
        };
        user.IsActive = true;
        user.PasswordHash = _passwordHasher.HashPassword(user, password);
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        _context.Users.Add(user);
        await SaveHandlingUsernameCollisionAsync(cancellationToken);
        await ReplaceRolesAsync(user.UserId, roles, cancellationToken);
        await EnsureManagerWouldRemainAsync(tenantId, user.UserId, WillManage(user, roles), cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(await RequireVisibleUserAsync(user.UserId, cancellationToken));
    }

    public async Task<UserResponse> UpdateAsync(
        long userId,
        SaveUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var user = await RequireVisibleUserAsync(userId, cancellationToken);
        var username = RequiredUsername(request.Username);
        await EnsureUsernameAvailableAsync(tenantId, username, user.UserId, cancellationToken);
        var link = await ResolveLinkAsync(request, tenantId, user.UserId, cancellationToken);
        var roles = await LoadRolesAsync(tenantId, request.RoleIds, link.Category, cancellationToken);

        user.Username = username;
        user.FirstName = RequiredName(request.FirstName, "First name");
        user.LastName = RequiredName(request.LastName, "Last name");
        user.Email = Clean(request.Email, 200);
        user.MobileNo = Clean(request.MobileNo, 20);
        user.UserCategory = link.Category;
        user.EmployeeId = link.EmployeeId;
        user.CustomerId = link.CustomerId;
        user.BranchId = link.BranchId;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            var password = RequiredPassword(request.Password);
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
        }

        await ReplaceRolesAsync(user.UserId, roles, cancellationToken);
        await EnsureManagerWouldRemainAsync(tenantId, user.UserId, WillManage(user, roles), cancellationToken);
        await SaveHandlingUsernameCollisionAsync(cancellationToken);
        return Map(await RequireVisibleUserAsync(user.UserId, cancellationToken));
    }

    public async Task<UserResponse> SetActiveAsync(
        long userId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var user = await RequireVisibleUserAsync(userId, cancellationToken);
        user.IsActive = isActive;
        var willManage = false;
        if (isActive && user.UserCategory != UserCategory.Customer)
        {
            var access = await AccessReader.ReadAccessAsync(_context, user.UserId, tenantId, cancellationToken);
            willManage = access.PermissionCodes.Contains(PermissionCatalog.UsersManage);
        }

        await EnsureManagerWouldRemainAsync(tenantId, user.UserId, willManage, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Map(await RequireVisibleUserAsync(user.UserId, cancellationToken));
    }

    public async Task<UserResponse> SetRolesAsync(
        long userId,
        IReadOnlyCollection<long> roleIds,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var user = await RequireVisibleUserAsync(userId, cancellationToken);
        var roles = await LoadRolesAsync(tenantId, roleIds, user.UserCategory, cancellationToken);
        await ReplaceRolesAsync(user.UserId, roles, cancellationToken);
        await EnsureManagerWouldRemainAsync(tenantId, user.UserId, WillManage(user, roles), cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Map(await RequireVisibleUserAsync(user.UserId, cancellationToken));
    }

    private IQueryable<User> VisibleUsers()
    {
        var query = _context.Users
            .AsNoTracking()
            .Include(user => user.Employee)
            .Include(user => user.Customer)
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .Where(user => !user.IsDeleted);

        if (_currentUser.BranchId is > 0)
        {
            var branchId = _currentUser.BranchId.Value;
            query = query.Where(user => user.BranchId == branchId);
        }

        return query;
    }

    private async Task<User> RequireVisibleUserAsync(long userId, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.RequireTenantId();
        var user = await _context.Users
            .IgnoreQueryFilters()
            .Include(item => item.Employee)
            .Include(item => item.Customer)
            .Include(item => item.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .FirstOrDefaultAsync(item => item.UserId == userId && !item.IsDeleted, cancellationToken);

        if (user == null || user.TenantId != tenantId)
        {
            throw new KeyNotFoundException();
        }

        if (_currentUser.BranchId is > 0 && user.BranchId != _currentUser.BranchId)
        {
            throw new UnauthorizedAccessException("You cannot access another branch.");
        }

        return user;
    }

    private async Task EnsureUsernameAvailableAsync(
        long tenantId,
        string username,
        long? exceptUserId,
        CancellationToken cancellationToken)
    {
        var taken = await _context.Users.IgnoreQueryFilters().AnyAsync(
            user => user.TenantId == tenantId &&
                    user.Username == username &&
                    !user.IsDeleted &&
                    user.UserId != exceptUserId,
            cancellationToken);

        if (taken)
        {
            throw new IdentityException("That username is already used in this salon.");
        }
    }

    private async Task<ResolvedLink> ResolveLinkAsync(
        SaveUserRequest request,
        long tenantId,
        long? exceptUserId,
        CancellationToken cancellationToken)
    {
        var category = ParseCategory(request.UserCategory);
        switch (category)
        {
            case UserCategory.General:
                if (request.EmployeeId is > 0 || request.CustomerId is > 0)
                {
                    throw new IdentityException("A general user cannot be linked to an employee or a customer.");
                }

                return new ResolvedLink(category, null, null, await ResolveGeneralBranchAsync(request.BranchId, tenantId, cancellationToken));

            case UserCategory.Employee:
                if (request.CustomerId is > 0)
                {
                    throw new IdentityException("An employee user cannot be linked to a customer.");
                }

                if (request.EmployeeId is not > 0)
                {
                    throw new IdentityException("An employee user must be linked to an employee.");
                }

                var employee = await _context.Employees.IgnoreQueryFilters().FirstOrDefaultAsync(
                    item => item.EmployeeId == request.EmployeeId && !item.IsDeleted,
                    cancellationToken);
                if (employee == null || employee.TenantId != tenantId)
                {
                    throw new IdentityException("The selected employee was not found in this salon.");
                }

                RejectOtherBranch(employee.BranchId);
                if (request.BranchId is > 0 && request.BranchId != employee.BranchId)
                {
                    throw new IdentityException("An employee user's branch comes from the employee record.");
                }

                await EnsureLinkFreeAsync(exceptUserId, employee.EmployeeId, null, cancellationToken);
                return new ResolvedLink(category, employee.EmployeeId, null, employee.BranchId);

            case UserCategory.Customer:
                if (request.EmployeeId is > 0)
                {
                    throw new IdentityException("A customer user cannot be linked to an employee.");
                }

                if (request.CustomerId is not > 0)
                {
                    throw new IdentityException("A customer user must be linked to a customer.");
                }

                var customer = await _context.Customers.IgnoreQueryFilters().FirstOrDefaultAsync(
                    item => item.CustomerId == request.CustomerId && !item.IsDeleted,
                    cancellationToken);
                if (customer == null || customer.TenantId != tenantId)
                {
                    throw new IdentityException("The selected customer was not found in this salon.");
                }

                RejectOtherBranch(customer.BranchId);
                if (request.BranchId is > 0 && request.BranchId != customer.BranchId)
                {
                    throw new IdentityException("A customer user's branch comes from the customer record.");
                }

                await EnsureLinkFreeAsync(exceptUserId, null, customer.CustomerId, cancellationToken);
                return new ResolvedLink(category, null, customer.CustomerId, customer.BranchId);

            default:
                throw new IdentityException("User category must be General, Employee, or Customer.");
        }
    }

    private async Task<long?> ResolveGeneralBranchAsync(
        long? requestedBranchId,
        long tenantId,
        CancellationToken cancellationToken)
    {
        if (_currentUser.BranchId is > 0)
        {
            if (requestedBranchId is > 0 && requestedBranchId != _currentUser.BranchId)
            {
                throw new IdentityException("You cannot assign a user to another branch.");
            }

            return _currentUser.BranchId;
        }

        if (requestedBranchId is not > 0)
        {
            return null;
        }

        var branch = await _context.Branches.IgnoreQueryFilters().FirstOrDefaultAsync(
            item => item.BranchId == requestedBranchId && item.TenantId == tenantId && !item.IsDeleted,
            cancellationToken);
        if (branch == null)
        {
            throw new IdentityException("The selected branch was not found in this salon.");
        }

        return branch.BranchId;
    }

    private void RejectOtherBranch(long branchId)
    {
        if (_currentUser.BranchId is > 0 && _currentUser.BranchId != branchId)
        {
            throw new UnauthorizedAccessException("You cannot access another branch.");
        }
    }

    private async Task EnsureLinkFreeAsync(
        long? exceptUserId,
        long? employeeId,
        long? customerId,
        CancellationToken cancellationToken)
    {
        if (employeeId is > 0)
        {
            var taken = await _context.Users.IgnoreQueryFilters().AnyAsync(
                user => user.EmployeeId == employeeId && user.UserId != exceptUserId,
                cancellationToken);
            if (taken)
            {
                throw new IdentityException("That employee already has a user account.");
            }
        }

        if (customerId is > 0)
        {
            var taken = await _context.Users.IgnoreQueryFilters().AnyAsync(
                user => user.CustomerId == customerId && user.UserId != exceptUserId,
                cancellationToken);
            if (taken)
            {
                throw new IdentityException("That customer already has a user account.");
            }
        }
    }

    private async Task<List<Role>> LoadRolesAsync(
        long tenantId,
        IReadOnlyCollection<long> roleIds,
        UserCategory category,
        CancellationToken cancellationToken)
    {
        var ids = roleIds.Where(id => id > 0).Distinct().ToList();
        var roles = await _context.Roles
            .IgnoreQueryFilters()
            .Include(role => role.RolePermissions)
            .ThenInclude(rolePermission => rolePermission.Permission)
            .Where(role => ids.Contains(role.RoleId) && role.TenantId == tenantId && !role.IsDeleted)
            .ToListAsync(cancellationToken);

        if (roles.Count != ids.Count)
        {
            throw new IdentityException("One or more roles are not available in this salon.");
        }

        if (category == UserCategory.Customer &&
            roles.SelectMany(role => role.RolePermissions).Any(item =>
                item.Permission != null && PermissionCatalog.IsStaffCode(item.Permission.PermissionCode)))
        {
            throw new IdentityException("A customer user cannot receive a role that includes staff permissions.");
        }

        await AccessReader.EnsureMayGrantAsync(
            _context,
            _currentUser,
            roles.SelectMany(StaffPermissionCodes),
            cancellationToken);

        return roles;
    }

    private static IEnumerable<string> StaffPermissionCodes(Role role)
    {
        return role.RolePermissions
            .Where(item => item.Permission is { IsDeleted: false })
            .Select(item => item.Permission.PermissionCode);
    }

    private async Task ReplaceRolesAsync(
        long userId,
        IReadOnlyCollection<Role> roles,
        CancellationToken cancellationToken)
    {
        var existing = await _context.UserRoles.Where(item => item.UserId == userId).ToListAsync(cancellationToken);
        var wanted = roles.Select(role => role.RoleId).ToHashSet();
        _context.UserRoles.RemoveRange(existing.Where(item => !wanted.Contains(item.RoleId)));
        var present = existing.Select(item => item.RoleId).ToHashSet();
        foreach (var role in roles.Where(role => !present.Contains(role.RoleId)))
        {
            _context.UserRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = role.RoleId
            });
        }
    }

    private async Task EnsureManagerWouldRemainAsync(
        long tenantId,
        long userId,
        bool willManage,
        CancellationToken cancellationToken)
    {
        var managers = await AccessReader.ManagerIdsAsync(_context, tenantId, cancellationToken);
        managers.Remove(userId);
        if (willManage)
        {
            managers.Add(userId);
        }

        if (managers.Count == 0)
        {
            throw new IdentityException("The salon must keep at least one active user who can manage users.");
        }
    }

    private static bool WillManage(User user, IEnumerable<Role> roles)
    {
        if (!user.IsActive || user.UserCategory == UserCategory.Customer)
        {
            return false;
        }

        return roles.Any(role =>
            role.IsActive &&
            !role.IsDeleted &&
            role.RolePermissions.Any(item =>
                item.Permission is { IsActive: true, IsDeleted: false } &&
                item.Permission.PermissionCode == PermissionCatalog.UsersManage));
    }

    private async Task SaveHandlingUsernameCollisionAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (TryGetLinkConstraintMessage(exception, out var message))
        {
            throw new IdentityException(message);
        }
    }

    private static bool TryGetLinkConstraintMessage(DbUpdateException exception, out string message)
    {
        var text = exception.InnerException?.Message ?? exception.Message;
        if (text.Contains("EmployeeId", StringComparison.OrdinalIgnoreCase))
        {
            message = "That employee already has a user account.";
            return true;
        }

        if (text.Contains("CustomerId", StringComparison.OrdinalIgnoreCase))
        {
            message = "That customer already has a user account.";
            return true;
        }

        if (text.Contains("IX_Users_Tenant_Username", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("Username", StringComparison.OrdinalIgnoreCase))
        {
            message = "That username is already used in this salon.";
            return true;
        }

        message = string.Empty;
        return false;
    }

    private static UserResponse Map(User user)
    {
        return new UserResponse
        {
            UserId = user.UserId,
            Username = user.Username,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Email = user.Email,
            MobileNo = user.MobileNo,
            UserCategory = user.UserCategory.ToString(),
            TenantId = user.TenantId,
            BranchId = user.BranchId,
            EmployeeId = user.EmployeeId,
            EmployeeName = user.Employee == null
                ? null
                : $"{user.Employee.FirstName} {user.Employee.LastName}".Trim(),
            CustomerId = user.CustomerId,
            CustomerName = user.Customer == null
                ? null
                : $"{user.Customer.FirstName} {user.Customer.LastName}".Trim(),
            IsActive = user.IsActive,
            Roles = user.UserRoles
                .Where(item => item.Role is { IsDeleted: false })
                .Select(item => new RoleSummaryResponse
                {
                    RoleId = item.Role.RoleId,
                    RoleCode = item.Role.RoleCode,
                    RoleName = item.Role.RoleName
                })
                .OrderBy(item => item.RoleName)
                .ToList()
        };
    }

    private static UserCategory ParseCategory(string? value)
    {
        if (string.Equals(value, nameof(UserCategory.General), StringComparison.OrdinalIgnoreCase))
        {
            return UserCategory.General;
        }

        if (string.Equals(value, nameof(UserCategory.Employee), StringComparison.OrdinalIgnoreCase))
        {
            return UserCategory.Employee;
        }

        if (string.Equals(value, nameof(UserCategory.Customer), StringComparison.OrdinalIgnoreCase))
        {
            return UserCategory.Customer;
        }

        throw new IdentityException("User category must be General, Employee, or Customer.");
    }

    private static string RequiredUsername(string? username)
    {
        var trimmed = username?.Trim() ?? string.Empty;
        if (trimmed.Length is < 1 or > 100)
        {
            throw new IdentityException("Username is required and must be 100 characters or fewer.");
        }

        return trimmed;
    }

    private static string RequiredPassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new IdentityException("Password must be at least 8 characters.");
        }

        return password;
    }

    private static string RequiredName(string? value, string label)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length is < 1 or > 100)
        {
            throw new IdentityException($"{label} is required and must be 100 characters or fewer.");
        }

        return trimmed;
    }

    private static string? Clean(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw new IdentityException($"A value is longer than {maxLength} characters.");
        }

        return trimmed;
    }

    private sealed record ResolvedLink(
        UserCategory Category,
        long? EmployeeId,
        long? CustomerId,
        long? BranchId);
}
