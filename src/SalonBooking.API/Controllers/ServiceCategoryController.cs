using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SalonBooking.API.Authorization;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Features.ServiceCategory.DTOs;
using SalonBooking.Application.Security;

namespace SalonBooking.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ServiceCategoriesController
    : ControllerBase
{
    private readonly IServiceCategoryService
        _serviceCategoryService;

    public ServiceCategoriesController(
        IServiceCategoryService serviceCategoryService)
    {
        _serviceCategoryService = serviceCategoryService;
    }

    [HttpPost]
    [RequiresPermission(PermissionCatalog.ServiceCategoriesManage)]
    public async Task<IActionResult> Create(CreateServiceCategoryRequest request)
    {
        var serviceCategory = await _serviceCategoryService.CreateAsync(request);

        return Ok(serviceCategory);
    }

   // [HttpGet]
  //  public async Task<IActionResult> GetAll()
  //  {
 //       var serviceCategories = await _serviceCategoryService.GetAllAsync();

 //       return Ok(serviceCategories);
 //   }

    [HttpGet("{id:long}")]
    [RequiresPermission(PermissionCatalog.ServiceCategoriesRead)]
    public async Task<IActionResult> GetById(long id)
    {
        var serviceCategory = await _serviceCategoryService.GetByIdAsync(id);

        if (serviceCategory == null)
            return NotFound();

        return Ok(serviceCategory);
    }

    [HttpGet]
    [RequiresPermission(PermissionCatalog.ServiceCategoriesRead)]
    public async Task<IActionResult> GetServiceCategories(
        [FromQuery] ServiceCategoryQueryRequest request)
    {
        var result = await _serviceCategoryService.GetServiceCategoriesAsync(request);

        return Ok(result);
    }

    [HttpPut("{id:long}")]
    [RequiresPermission(PermissionCatalog.ServiceCategoriesManage)]
    public async Task<IActionResult> Update(
        long id,
        UpdateServiceCategoryRequest request)
    {
        var serviceCategory =
            await _serviceCategoryService.UpdateAsync(id, request);

        if (serviceCategory == null)
            return NotFound();    

        return Ok(serviceCategory);
    }

    [HttpDelete("{id:long}")]
    [RequiresPermission(PermissionCatalog.ServiceCategoriesManage)]
    public async Task<IActionResult> Delete(long id)
    {
        await _serviceCategoryService.DeleteAsync(id);

        return NoContent();
    }
}