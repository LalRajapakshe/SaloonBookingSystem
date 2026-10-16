using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonBooking.API.Authorization;
using SalonBooking.Application.Security;

namespace SalonBooking.API.Controllers;

[Authorize]
[ApiController]
[Route("api/test")]
public class TestController
    : ControllerBase
{
    [HttpGet]
    [RequiresPermission(PermissionCatalog.UsersRead)]
    public IActionResult Get()
    {
        return Ok(
            "JWT Authentication Works!");
    }
}