namespace SalonBooking.Application.Features.Employee.DTOs;

public class EmployeeResponse
{
    public long EmployeeId { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;

   public string FullName { get; set; }  = string.Empty;

    public string MobileNo { get; set; }= string.Empty;

    public string? Email { get; set; }

    public string? Gender { get; set; }

      //    public DateTime? HireDate { get; set; }

    public string? Designation { get; set; }

  //  public decimal Salary { get; set; }

     public long BranchId { get; set; } 

    public long TenantId { get; set; }

}