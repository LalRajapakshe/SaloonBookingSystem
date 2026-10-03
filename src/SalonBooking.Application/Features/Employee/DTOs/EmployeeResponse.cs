namespace SalonBooking.Application.Features.Employee.DTOs;

public class EmployeeResponse
{
    public long EmployeeId { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string MobileNo { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public string? Address { get; set; }

    public DateTime? HireDate { get; set; }

    public string? Designation { get; set; }

    public decimal Salary { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedDate { get; set; }

    public long BranchId { get; set; }

    public long TenantId { get; set; }

    public static EmployeeResponse From(SalonBooking.Domain.Entities.Employee employee)
    {
        return new EmployeeResponse
        {
            EmployeeId = employee.EmployeeId,
            EmployeeCode = employee.EmployeeCode,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            FullName = $"{employee.FirstName} {employee.LastName}".Trim(),
            MobileNo = employee.MobileNo,
            Email = employee.Email,
            Gender = employee.Gender,
            DateOfBirth = employee.DateOfBirth,
            Address = employee.Address,
            HireDate = employee.HireDate,
            Designation = employee.Designation,
            Salary = employee.Salary,
            IsActive = employee.IsActive,
            CreatedDate = employee.CreatedDate,
            TenantId = employee.TenantId,
            BranchId = employee.BranchId
        };
    }
}
