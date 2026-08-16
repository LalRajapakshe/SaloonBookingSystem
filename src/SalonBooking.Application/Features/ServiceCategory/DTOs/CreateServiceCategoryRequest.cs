namespace SalonBooking.Application.Features.ServiceCategory.DTOs;

public class CreateServiceCategoryRequest
{
  // public int BranchId { get; set; }

    public string CategoryName { get; set; }  = string.Empty;

    public string Description { get; set; }  = string.Empty;

    public int DisplayOrder { get; set; }

}
