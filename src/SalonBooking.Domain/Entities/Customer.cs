namespace SalonBooking.Domain.Entities
{
public class Customer
{
    public long CustomerId { get; set; }

    public long TenantId { get; set; }

    public string CustomerCode { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string MobileNo { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public string? Remarks { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedDate { get; set; }
        = DateTime.UtcNow;
}

}