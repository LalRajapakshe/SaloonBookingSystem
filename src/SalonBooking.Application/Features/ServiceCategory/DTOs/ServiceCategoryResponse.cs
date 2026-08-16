namespace SalonBooking.Application.Features.ServiceCategory.DTOs;

public class ServiceCategoryResponse
{
    public long ServiceCategoryId { get; set; }

    public string CategoryCode { get; set; }  = string.Empty;

    public string CategoryName { get; set; }  = string.Empty;

    public string Description { get; set; }  = string.Empty;

    public int DisplayOrder { get; set; }

   // public long BranchId { get; set; } 

    public long TenantId { get; set; }

}