namespace SalonBooking.Application.Features.Tenant.DTOs;

public class CreateTenantRequest
{
    public string TenantName { get; set; } = string.Empty;

    public string BusinessName { get; set; } = string.Empty;

    public string? BusinessRegistrationNo { get; set; }

    public string? ContactPerson { get; set; }

    public string? PhoneNo { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? LogoUrl { get; set; }

    public int MaxBranches { get; set; }

    public int MaxUsers { get; set; }
}