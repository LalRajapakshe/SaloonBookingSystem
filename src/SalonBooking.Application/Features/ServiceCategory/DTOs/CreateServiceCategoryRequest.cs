namespace SalonBooking.Application.Features.ServiceCategory.DTOs;

public class CreateServiceCategoryRequest
{
    public string CategoryName { get; set; }  = string.Empty;

    public string Description { get; set; }  = string.Empty;

    public string DisplayOrder { get; set; }= string.Empty;

}
