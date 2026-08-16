namespace SalonBooking.Application.Features.Service.DTOs;

public class ServiceResponse
{
    public long ServiceId { get; set; }

    public string ServiceCode { get; set; }  = string.Empty;

    public string ServiceName { get; set; }  = string.Empty;

    public string Description { get; set; }  = string.Empty;

    public int  DurationMinutes { get; set; }

    public decimal Price { get; set; }

    public decimal Cost { get; set; }

    public decimal CommissionPercentage { get; set; }

    public long BranchId { get; set; } 

    public long TenantId { get; set; }

}