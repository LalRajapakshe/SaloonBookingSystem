using SalonBooking.Application.Features.Employee.DTOs;
using SalonBooking.Application.Features.Employee;
using SalonBooking.Domain.Entities;
using SalonBooking.Application.Common;

namespace SalonBooking.Application.Interfaces
{
public interface IEmployeeService
{
    Task<EmployeeResponse> CreateAsync(
        CreateEmployeeRequest request);

        Task<PagedResult<EmployeeResponse>> GetEmployeesAsync(EmployeeQueryRequest request);

    Task<List<EmployeeResponse>> GetAllAsync();

    Task<EmployeeResponse?> GetByIdAsync(
        long employeeId);

    Task<EmployeeResponse> UpdateAsync(
        long employeeId,
        UpdateEmployeeRequest request);

    Task DeleteAsync(
        long employeeId);
}
}