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

    private readonly ICustomerPhotoService _photos;

    public CustomersController(
        ICustomerService customerService,
        ICustomerPhotoService photos)
    {
        _customerService = customerService;
        _photos = photos;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateCustomerRequest request)
    {
        var customer = await _customerService.CreateAsync(request);

        return Ok(customer);
    }

   // [HttpGet]
  //  public async Task<IActionResult> GetAll()
  //  {
 //       var customers = await _customerService.GetAllAsync();

 //       return Ok(customers);
 //   }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var customer = await _customerService.GetByIdAsync(id);

        if (customer == null)
            return NotFound();

        return Ok(customer);
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] CustomerQueryRequest request)
    {
        var result = await _customerService.GetCustomersAsync(request);

        return Ok(result);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(
        long id,
        UpdateCustomerRequest request)
    {
        var customer =
            await _customerService.UpdateAsync(id, request);

        if (customer == null)
            return NotFound();    

        return Ok(customer);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _customerService.DeleteAsync(id);

        return NoContent();
    }

    [HttpPost("{id:long}/photo")]
    [RequestSizeLimit(5_242_880)]
    public async Task<IActionResult> SavePhoto(
        long id,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { title = "A photo file is required." });
        }

        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        var customer = await _photos.SaveAsync(id, buffer.ToArray(), cancellationToken);
        return Ok(customer);
    }

    [HttpGet("{id:long}/photo")]
    public async Task<IActionResult> GetPhoto(long id, CancellationToken cancellationToken)
    {
        var photo = await _photos.OpenAsync(id, cancellationToken);
        if (photo == null)
        {
            return NotFound(new { title = "Not Found" });
        }

        return File(photo.Content, photo.ContentType);
    }

    [HttpDelete("{id:long}/photo")]
    public async Task<IActionResult> RemovePhoto(long id, CancellationToken cancellationToken)
    {
        return Ok(await _photos.RemoveAsync(id, cancellationToken));
    }
}