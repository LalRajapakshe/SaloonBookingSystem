using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Features.Employee.DTOs;

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
    public async Task<IActionResult> GetById(long id)
    {
        var employee = await _employeeService.GetByIdAsync(id);

        if (employee == null)
            return NotFound();

        return Ok(employee);
    }

    [HttpGet]
    public async Task<IActionResult> GetEmployees(
        [FromQuery] EmployeeQueryRequest request)
    {
        var result = await _employeeService.GetEmployeesAsync(request);

        return Ok(result);
    }

    [HttpPut("{id:long}")]
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
    public async Task<IActionResult> Delete(long id)
    {
        await _employeeService.DeleteAsync(id);

        return NoContent();
    }
}