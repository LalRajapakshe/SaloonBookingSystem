namespace SalonBooking.Application.Features.ServiceCategory.DTOs;
public class UpdateServiceCategoryRequest
    
{
     //  public string EmployeeCode { get; set } = string.Empty;

    public string CategoryName { get; set; }  = string.Empty;

    public string Description { get; set; }  = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }

   // public long BranchId { get; set; }   
} 