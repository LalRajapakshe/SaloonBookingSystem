namespace SalonBooking.Application.Features.Service.DTOs;

public class CreateServiceRequest
{
    public string ServiceName { get; set; }  = string.Empty;

    public string Description { get; set; }  = string.Empty;

    public int  DurationMinutes { get; set; }= string.Empty;

    public decimal Price { get; set; }

    public decimal Cost { get; set; }

    public decimal CommissionPercentage { get; set; }

}
