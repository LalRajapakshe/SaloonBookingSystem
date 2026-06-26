namespace SalonBooking.Application.Features.Customer.DTOs;

public class CreateCustomerRequest
{
public string FirstName { get; set; }
        = string.Empty;

    public string LastName { get; set; }
        = string.Empty;

    public string MobileNo { get; set; }
        = string.Empty;

    public string? Email { get; set; }

    public string? Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public string? Remarks { get; set; }
}