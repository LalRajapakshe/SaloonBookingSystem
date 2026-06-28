using SalonBooking.Domain.Common;

namespace SalonBooking.Domain.Entities;

public class Branch : TenantEntity
{
    public long BranchId { get; set; }

    public string BranchCode { get; set; } = string.Empty;

    public string BranchName { get; set; } = string.Empty;

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    public string? PhoneNo { get; set; }

    public string? Email { get; set; }

    public string? ManagerName { get; set; }

    public bool IsHeadOffice { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; } = false;

    public Tenant Tenant { get; set; } = null!;
}