using SalonBooking.Domain.Common;

namespace SalonBooking.Domain.Entities;

public class Employee : TenantEntity
{
    public long EmployeeId { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;

    public long BranchId { get; set; }

    public long? UserId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string MobileNo { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public string? Address { get; set; }

    public DateTime? HireDate { get; set; }

    public string? Designation { get; set; }

    public decimal Salary { get; set; }

    public Branch Branch { get; set; } = null!;

    public Tenant Tenant { get; set; } = null!;

    public User? User { get; set; }
}