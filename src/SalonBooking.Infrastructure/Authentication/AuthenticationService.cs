using SalonBooking.Application.Features.Authentication.DTOs;
using SalonBooking.Application.Interfaces;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Infrastructure.Authentication;

public class AuthenticationService : IAuthenticationService
{
    private readonly IJwtTokenService _jwtTokenService;

    public AuthenticationService(
        IJwtTokenService jwtTokenService)
    {
        _jwtTokenService = jwtTokenService;
    }

    public Task<LoginResponse> LoginAsync(
        LoginRequest request)
    {
        if (request.Username != "admin" ||
    request.Password != "Admin@123")
{
    throw new UnauthorizedAccessException(
        "Invalid username or password.");
}

var user = new User
{
    UserId = 1,
    Username = "admin",
    PasswordHash = "",
    TenantId = 1
};

var token =
    _jwtTokenService.GenerateToken(user);

var response =
    new LoginResponse
    {
        AccessToken = token,
        Username = "admin",
        FullName = "System Administrator",
        ExpiresAt =
            DateTime.UtcNow.AddHours(8)
    };

return Task.FromResult(response);
    }
}