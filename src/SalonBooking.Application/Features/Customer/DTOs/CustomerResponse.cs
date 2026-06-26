namespace SalonBooking.Application.Features.Customer.DTOs;

public class CustomerResponse
{
    public long CustomerId { get; set; }

    public string CustomerCode { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string MobileNo { get; set; } = string.Empty;

    public string? Email { get; set; }
}