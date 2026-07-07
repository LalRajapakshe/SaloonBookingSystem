namespace SalonBooking.Application.Features.Employee.DTOs;
public class UpdateEmployeeRequest
    
{
     //  public string EmployeeCode { get; set } = string.Empty;

   public long BranchId { get; set; }

    public string FirstName { get; set; }  = string.Empty;

    public string LastName { get; set; }  = string.Empty;

    public string MobileNo { get; set; }= string.Empty;

    public string? Email { get; set; }

    public string? Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public string? Address { get; set; }

    public DateTime? HireDate { get; set; }

    public string? Designation { get; set; }

    public decimal Salary { get; set; }
    
    public bool IsActive { get; set; }
}