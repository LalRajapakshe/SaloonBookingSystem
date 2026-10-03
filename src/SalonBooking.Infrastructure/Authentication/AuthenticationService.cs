using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
    private readonly JwtSettings _jwtSettings;

    public AuthenticationService(
        SalonBookingDbContext context,
        IJwtTokenService jwtTokenService,
        IPasswordHasher<User> passwordHasher,
        IOptions<JwtSettings> jwtOptions)
    {
        _context = context;
        _jwtTokenService = jwtTokenService;
        _passwordHasher = passwordHasher;
        _jwtSettings = jwtOptions.Value;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var username = request.Username.Trim();
        var users = await _context.Users
            .IgnoreQueryFilters()
            .Where(user =>
                user.Username == username &&
                user.IsActive &&
                !user.IsDeleted)
            .Take(2)
            .ToListAsync();

        if (users.Count != 1)
        {
            throw new UnauthorizedAccessException(
                "Invalid username or password.");
        }

        var user = users[0];
        var verification = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedAccessException(
                "Invalid username or password.");
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(
                user,
                request.Password);
        }

        user.LastLoginDate = DateTime.UtcNow;

        var roles = await _context.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.UserId == user.UserId)
            .Join(
                _context.Roles.IgnoreQueryFilters(),
                userRole => userRole.RoleId,
                role => role.RoleId,
                (userRole, role) => role)
            .Where(role => role.IsActive && !role.IsDeleted)
            .Select(role => role.RoleName)
            .ToListAsync();

        await _context.SaveChangesAsync();

        return new LoginResponse
        {
            AccessToken = _jwtTokenService.GenerateToken(user, roles),
            Username = user.Username,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes)
        };
    }
}
