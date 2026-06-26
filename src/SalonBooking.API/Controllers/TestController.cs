using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SalonBooking.API.Controllers;

[Authorize]
[ApiController]
[Route("api/test")]
public class TestController
    : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(
            "JWT Authentication Works!");
    }
}