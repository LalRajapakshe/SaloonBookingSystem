using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Features.Customer.DTOs;

namespace SalonBooking.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CustomersController
    : ControllerBase
{
    private readonly ICustomerService
        _customerService;

    public CustomersController(
        ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateCustomerRequest request)
    {
        var customer = await _customerService.CreateAsync(request);

        return Ok(customer);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var customers = await _customerService.GetAllAsync();

        return Ok(customers);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var customer = await _customerService.GetByIdAsync(id);

        if (customer == null)
            return NotFound();

        return Ok(customer);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(
        long id,
        UpdateCustomerRequest request)
    {
        var customer =
            await _customerService.UpdateAsync(id, request);

        return Ok(customer);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _customerService.DeleteAsync(id);

        return NoContent();
    }
}