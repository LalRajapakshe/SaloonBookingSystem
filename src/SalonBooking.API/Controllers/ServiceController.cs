using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Features.Service.DTOs;

namespace SalonBooking.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ServicesController
    : ControllerBase
{
    private readonly IServiceService
        _serviceService;

    public ServicesController(
        IServiceService serviceService)
    {
        _serviceService = serviceService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateServiceRequest request)
    {
        var service = await _serviceService.CreateAsync(request);

        return Ok(service);
    }

   // [HttpGet]
  //  public async Task<IActionResult> GetAll()
  //  {
 //       var services = await _serviceService.GetAllAsync();

 //       return Ok(services);
 //   }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        var service = await _serviceService.GetByIdAsync(id);

        if (service == null)
            return NotFound();

        return Ok(service);
    }

    [HttpGet]
    public async Task<IActionResult> GetServices(
        [FromQuery] ServiceQueryRequest request)
    {
        var result = await _serviceService.GetServicesAsync(request);

        return Ok(result);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(
        long id,
        UpdateServiceRequest request)
    {
        var service =
            await _serviceService.UpdateAsync(id, request);

        if (service == null)
            return NotFound();    

        return Ok(service);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        await _serviceService.DeleteAsync(id);

        return NoContent();
    }
}