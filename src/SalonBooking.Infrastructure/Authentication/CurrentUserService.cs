using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SalonBooking.Application.Interfaces;

namespace SalonBooking.Infrastructure.Authentication;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public long? UserId =>
        TryGetInt64(ClaimTypes.NameIdentifier)
        ?? TryGetInt64(JwtRegisteredClaimNames.Sub);

    public long? TenantId => TryGetInt64("TenantId");

    public long? BranchId => TryGetInt64("BranchId");

    public string? Username =>
        TryGet(ClaimTypes.Name)
        ?? TryGet(JwtRegisteredClaimNames.UniqueName);

    public string? Role =>
        TryGet(ClaimTypes.Role)
        ?? TryGet("role");

    private string? TryGet(string claimType)
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirst(claimType)?.Value;
    }

    private long? TryGetInt64(string claimType)
    {
        var value = TryGet(claimType);
        return long.TryParse(value, out var parsed) ? parsed : null;
    }
}
