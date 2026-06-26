using SalonBooking.Application.Features.Authentication.DTOs;

namespace SalonBooking.Application.Interfaces;

public interface IAuthenticationService
{
    Task<LoginResponse> LoginAsync(
        LoginRequest request);
}