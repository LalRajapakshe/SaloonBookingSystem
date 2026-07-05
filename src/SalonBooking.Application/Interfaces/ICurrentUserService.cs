using SalonBooking.Application.Common;
//using SalonBooking.Application.Features.Branch.DTOs;

using SalonBooking.Application.Features.Tenant;
using SalonBooking.Domain.Entities;


namespace SalonBooking.Application.Interfaces;

public interface ICurrentUserService
{
    long UserId { get; }

    long TenantId { get; }

    long BranchId { get; }

    string Username { get; }

    string Role { get; }
}