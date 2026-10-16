using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SalonBooking.API.Authorization;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Features.Employee.DTOs;
using SalonBooking.Application.Security;

namespace SalonBooking.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EmployeesController
    : ControllerBase
{
    private readonly IEmployeeService
        _employeeService;

    public EmployeesController(
        IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    [HttpPost]
    [RequiresPermission(PermissionCatalog.EmployeesManage)]
    public async Task<IActionResult> Create(CreateEmployeeRequest request)
    {
        var employee = await _employeeService.CreateAsync(request);

        return Ok(employee);
    }

   // [HttpGet]
  //  public async Task<IActionResult> GetAll()
  //  {
 //       var employees = await _employeeService.GetAllAsync();

 //       return Ok(customers);
 //   }

    [HttpGet("{id:long}")]
    [RequiresPermission(PermissionCatalog.EmployeesRead)]
    public async Task<IActionResult> GetById(long id)
    {
        var employee = await _employeeService.GetByIdAsync(id);

        if (employee == null)
            return NotFound();

        return Ok(employee);
    }

    [HttpGet]
    [RequiresPermission(PermissionCatalog.EmployeesRead)]
    public async Task<IActionResult> GetEmployees(
        [FromQuery] EmployeeQueryRequest request)
    {
        var result = await _employeeService.GetEmployeesAsync(request);

        return Ok(result);
    }

    [HttpPut("{id:long}")]
    [RequiresPermission(PermissionCatalog.EmployeesManage)]
    public async Task<IActionResult> Update(
        long id,
        UpdateEmployeeRequest request)
    {
        var employee =
            await _employeeService.UpdateAsync(id, request);

        if (employee == null)
            return NotFound();    

        return Ok(employee);
    }

    [HttpDelete("{id:long}")]
    [RequiresPermission(PermissionCatalog.EmployeesManage)]
    public async Task<IActionResult> Delete(long id)
    {
        await _employeeService.DeleteAsync(id);

        return NoContent();
    }
}