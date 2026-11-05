using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SalonBooking.API.Authorization;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Features.Service.DTOs;
using SalonBooking.Application.Security;
using SalonBooking.Infrastructure.Services;

namespace SalonBooking.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ServicesController
    : ControllerBase
{
    private readonly IServiceService
        _serviceService;
    private readonly CatalogueImageAdminService _photos;

    public ServicesController(
        IServiceService serviceService,
        CatalogueImageAdminService photos)
    {
        _serviceService = serviceService;
        _photos = photos;
    }

    [HttpPost]
    [RequiresPermission(PermissionCatalog.ServicesManage)]
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
    [RequiresPermission(PermissionCatalog.ServicesRead)]
    public async Task<IActionResult> GetById(long id)
    {
        var service = await _serviceService.GetByIdAsync(id);

        if (service == null)
            return NotFound();

        return Ok(service);
    }

    [HttpGet]
    [RequiresPermission(PermissionCatalog.ServicesRead)]
    public async Task<IActionResult> GetServices(
        [FromQuery] ServiceQueryRequest request)
    {
        var result = await _serviceService.GetServicesAsync(request);

        return Ok(result);
    }

    [HttpPut("{id:long}")]
    [RequiresPermission(PermissionCatalog.ServicesManage)]
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
    [RequiresPermission(PermissionCatalog.ServicesManage)]
    public async Task<IActionResult> Delete(long id)
    {
        await _serviceService.DeleteAsync(id);

        return NoContent();
    }

    [HttpPost("{id:long}/photo")]
    [RequiresPermission(PermissionCatalog.ServicesManage)]
    [RequestSizeLimit(5_242_880)]
    public async Task<IActionResult> SavePhoto(long id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { title = "A photo file is required." });
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return Ok(await _photos.SaveServicePhotoAsync(id, buffer.ToArray(), cancellationToken));
    }

    [HttpGet("{id:long}/photo")]
    [RequiresPermission(PermissionCatalog.ServicesRead)]
    public async Task<IActionResult> Photo(long id, CancellationToken cancellationToken)
    {
        var photo = await _photos.OpenServicePhotoAsync(id, cancellationToken);
        return photo == null ? NotFound() : File(photo.Content, photo.ContentType);
    }

    [HttpDelete("{id:long}/photo")]
    [RequiresPermission(PermissionCatalog.ServicesManage)]
    public async Task<IActionResult> RemovePhoto(long id, CancellationToken cancellationToken)
    {
        return Ok(await _photos.RemoveServicePhotoAsync(id, cancellationToken));
    }
}