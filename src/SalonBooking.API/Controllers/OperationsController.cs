using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonBooking.API.Authorization;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Security;

namespace SalonBooking.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OperationsController : ControllerBase
{
    private readonly IOperationsService _operations;

    public OperationsController(IOperationsService operations)
    {
        _operations = operations;
    }

    [HttpGet("dashboard")]
    [RequiresAnyPermission(RequiresAnyPermissionAttribute.DashboardPolicy)]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        return Ok(await _operations.GetDashboardAsync(cancellationToken));
    }
}
