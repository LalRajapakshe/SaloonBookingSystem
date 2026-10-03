namespace SalonBooking.Application.Features.Service.DTOs;

public class ServiceResponse
{
    public long ServiceId { get; set; }

    public string ServiceCode { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int DurationMinutes { get; set; }

    public decimal Price { get; set; }

    public decimal Cost { get; set; }

    public decimal CommissionPercentage { get; set; }

    public long ServiceCategoryId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public long BranchId { get; set; }

    public long TenantId { get; set; }

    public static ServiceResponse From(SalonBooking.Domain.Entities.Service service)
    {
        return new ServiceResponse
        {
            ServiceId = service.ServiceId,
            ServiceCode = service.ServiceCode,
            ServiceName = service.ServiceName,
            Description = service.Description ?? string.Empty,
            DurationMinutes = service.DurationMinutes,
            Price = service.Price,
            Cost = service.Cost,
            CommissionPercentage = service.CommissionPercentage,
            ServiceCategoryId = service.ServiceCategoryId,
            IsActive = service.IsActive,
            CreatedDate = service.CreatedDate,
            BranchId = service.BranchId,
            TenantId = service.TenantId
        };
    }
}
