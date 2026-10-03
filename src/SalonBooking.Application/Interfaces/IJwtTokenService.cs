using SalonBooking.Domain.Entities;

namespace SalonBooking.Application.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(
        User user,
        IReadOnlyCollection<string> roles);
}
