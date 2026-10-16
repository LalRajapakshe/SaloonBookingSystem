using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalonBooking.Application.Interfaces;
using SalonBooking.Persistence.Context;

namespace SalonBooking.API.Authorization;

public sealed class ActiveAccountMiddleware : IMiddleware
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ICurrentUserService _currentUser;

    public ActiveAccountMiddleware(IServiceScopeFactory scopes, ICurrentUserService currentUser)
    {
        _scopes = scopes;
        _currentUser = currentUser;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (_currentUser.IsAuthenticated && _currentUser.UserId is long userId)
        {
            await using var scope = _scopes.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<SalonBookingDbContext>();
            var account = await database.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(user => user.UserId == userId && !user.IsDeleted)
                .Select(user => new { user.IsActive, user.TenantId, user.BranchId })
                .FirstOrDefaultAsync();

            if (account is not { IsActive: true })
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { title = "This account is inactive." });
                return;
            }

            if (_currentUser.TenantId != account.TenantId)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { title = "This sign-in does not match the account." });
                return;
            }

            context.User = WithDatabaseScope(context.User, account.TenantId, account.BranchId);
        }

        await next(context);
    }

    private static ClaimsPrincipal WithDatabaseScope(ClaimsPrincipal principal, long tenantId, long? branchId)
    {
        var source = principal.Identity as ClaimsIdentity;
        var identity = new ClaimsIdentity(
            source?.AuthenticationType,
            source?.NameClaimType ?? ClaimTypes.Name,
            source?.RoleClaimType ?? ClaimTypes.Role);

        foreach (var claim in principal.Claims)
        {
            if (claim.Type is "TenantId" or "BranchId")
            {
                continue;
            }

            identity.AddClaim(claim);
        }

        identity.AddClaim(new Claim("TenantId", tenantId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        if (branchId is > 0)
        {
            identity.AddClaim(new Claim("BranchId", branchId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        return new ClaimsPrincipal(identity);
    }
}
