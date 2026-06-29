namespace SalonBooking.Application.Features.Branch.DTOs;

public class BranchResponse
{
    public long TenantId { get; set; }

   // public long BranchId { get; set; }

    public string BranchCode { get; set; } = string.Empty;

    public bool IsActive { get; set; }  

    public string BranchName { get; set; } = string.Empty;

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    public string? PhoneNo { get; set; }

    public string? Email { get; set; }

    public string? ManagerName { get; set; }

    public bool IsHeadOffice { get; set; }
}


   