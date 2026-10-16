using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SalonBooking.Application.Common;
using SalonBooking.Application.Common.Settings;
using SalonBooking.Application.Features.Authentication.DTOs;
using SalonBooking.Application.Interfaces;
using SalonBooking.Domain.Entities;
using SalonBooking.Persistence.Context;

namespace SalonBooking.Infrastructure.Authentication;

public class AuthenticationService : IAuthenticationService
{
    private readonly SalonBookingDbContext _context;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ICurrentUserService _currentUser;
    private readonly JwtSettings _jwtSettings;

    public AuthenticationService(
        SalonBookingDbContext context,
        IJwtTokenService jwtTokenService,
        IPasswordHasher<User> passwordHasher,
        ICurrentUserService currentUser,
        IOptions<JwtSettings> jwtOptions)
    {
        _context = context;
        _jwtTokenService = jwtTokenService;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
        _jwtSettings = jwtOptions.Value;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var username = request.Username.Trim();
        var query = _context.Users
            .IgnoreQueryFilters()
            .Where(user =>
                user.Username == username &&
                user.IsActive &&
                !user.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.TenantCode))
        {
            var tenantCode = request.TenantCode.Trim();
            query = query.Where(user =>
                _context.Tenants.IgnoreQueryFilters().Any(tenant =>
                    tenant.TenantId == user.TenantId &&
                    tenant.TenantCode == tenantCode &&
                    !tenant.IsDeleted));
        }

        var users = await query.Take(2).ToListAsync();
        if (users.Count == 0)
        {
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        if (users.Count > 1)
        {
            throw new IdentityException(
                "More than one account uses this username. Enter the tenant code.");
        }

        var user = users[0];
        var verification = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        }

        user.LastLoginDate = DateTime.UtcNow;
        var profile = await AccessReader.ReadProfileAsync(_context, user, CancellationToken.None);
        await _context.SaveChangesAsync();

        return new LoginResponse
        {
            AccessToken = _jwtTokenService.GenerateToken(user, profile.Roles),
            Username = user.Username,
            FullName = profile.FullName,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
            Profile = profile
        };
    }

    public async Task<CurrentUserResponse> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.UserId is not > 0)
        {
            throw new UnauthorizedAccessException("Sign in is required.");
        }

        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                item => item.UserId == _currentUser.UserId && !item.IsDeleted,
                cancellationToken);

        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("This account is inactive.");
        }

        return await AccessReader.ReadProfileAsync(_context, user, cancellationToken);
    }
}
