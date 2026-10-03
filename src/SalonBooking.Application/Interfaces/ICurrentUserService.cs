namespace SalonBooking.Application.Interfaces;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }

    long? UserId { get; }

    long? TenantId { get; }

    long? BranchId { get; }

    string? Username { get; }

    string? Role { get; }
}
