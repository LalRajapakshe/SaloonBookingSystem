namespace SalonBooking.Application.Features.ServiceCategory.DTOs;

public class ServiceCategoryResponse
{

    public string CategoryName { get; set; }  = string.Empty;

    public string Description { get; set; }  = string.Empty;

    public string DisplayOrder { get; set; }= string.Empty;

    public long BranchId { get; set; } 

    public long TenantId { get; set; }

}