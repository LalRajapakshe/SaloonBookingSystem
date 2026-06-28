namespace SalonBooking.Application.Features.Tenant.DTOs;

public class TenantResponse
{
    public long TenantId { get; set; }

    public string TenantCode { get; set; } = string.Empty;

    public string TenantName { get; set; } = string.Empty;

    public string BusinessName { get; set; } = string.Empty;

    public string? ContactPerson { get; set; }

    public string? PhoneNo { get; set; }

    public string? Email { get; set; }

    public bool IsActive { get; set; }

    public int MaxBranches { get; set; }

    public int MaxUsers { get; set; }
}