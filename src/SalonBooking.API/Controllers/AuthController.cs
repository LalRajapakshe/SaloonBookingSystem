using Microsoft.AspNetCore.Mvc;

using SalonBooking.Application.Features.Authentication.DTOs;
using SalonBooking.Application.Interfaces;


namespace SalonBooking.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService
        _authenticationService;

    public AuthController(
        IAuthenticationService authenticationService)
    {
        _authenticationService =
            authenticationService;
    }    
[HttpPost("login")]
public async Task<IActionResult> Login(
    LoginRequest request)
{
    var response =
        await _authenticationService
            .LoginAsync(request);

    return Ok(response);
}
}