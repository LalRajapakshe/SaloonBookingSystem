using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Features.ServiceCategory.DTOs;

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
    public async Task<IActionResult> GetById(long id)
    {
        var serviceCategory = await _serviceCategoryService.GetByIdAsync(id);

        if (serviceCategory == null)
            return NotFound();

        return Ok(serviceCategory);
    }

    [HttpGet]
    public async Task<IActionResult> GetServiceCategories(
        [FromQuery] ServiceCategoryQueryRequest request)
    {
        var result = await _serviceCategoryService.GetServiceCategoriesAsync(request);

        return Ok(result);
    }

    [HttpPut("{id:long}")]
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
    public async Task<IActionResult> Delete(long id)
    {
        await _serviceCategoryService.DeleteAsync(id);

        return NoContent();
    }
}