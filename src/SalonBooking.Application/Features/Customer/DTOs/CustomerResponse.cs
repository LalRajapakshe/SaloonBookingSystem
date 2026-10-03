namespace SalonBooking.Application.Features.Customer.DTOs;

public class CustomerResponse
{
    public long CustomerId { get; set; }

    public string CustomerCode { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string MobileNo { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public string? Remarks { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public long TenantId { get; set; }

    public long BranchId { get; set; }

    public static CustomerResponse From(SalonBooking.Domain.Entities.Customer customer)
    {
        return new CustomerResponse
        {
            CustomerId = customer.CustomerId,
            CustomerCode = customer.CustomerCode,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            FullName = $"{customer.FirstName} {customer.LastName}".Trim(),
            MobileNo = customer.MobileNo,
            Email = customer.Email,
            Gender = customer.Gender,
            DateOfBirth = customer.DateOfBirth,
            Remarks = customer.Remarks,
            IsActive = customer.IsActive,
            CreatedDate = customer.CreatedDate,
            TenantId = customer.TenantId,
            BranchId = customer.BranchId
        };
    }
}
