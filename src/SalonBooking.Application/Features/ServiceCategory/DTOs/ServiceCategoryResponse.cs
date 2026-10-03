namespace SalonBooking.Application.Features.ServiceCategory.DTOs;

public class ServiceCategoryResponse
{
    public long ServiceCategoryId { get; set; }

    public string CategoryCode { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public long TenantId { get; set; }

    public static ServiceCategoryResponse From(SalonBooking.Domain.Entities.ServiceCategory category)
    {
        return new ServiceCategoryResponse
        {
            ServiceCategoryId = category.ServiceCategoryId,
            CategoryCode = category.CategoryCode,
            CategoryName = category.CategoryName,
            Description = category.Description ?? string.Empty,
            DisplayOrder = category.DisplayOrder,
            IsActive = category.IsActive,
            CreatedDate = category.CreatedDate,
            TenantId = category.TenantId
        };
    }
}
