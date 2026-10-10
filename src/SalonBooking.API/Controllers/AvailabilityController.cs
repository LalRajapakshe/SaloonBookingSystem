using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonBooking.Application.Features.Scheduling;
using SalonBooking.Application.Interfaces;

namespace SalonBooking.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AvailabilityController : ControllerBase
{
    private readonly IAvailabilityQueryService _availability;

    public AvailabilityController(IAvailabilityQueryService availability)
    {
        _availability = availability;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] AvailabilityQueryRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _availability.GetAvailabilityAsync(request, cancellationToken));
    }
}
