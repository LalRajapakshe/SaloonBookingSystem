using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonBooking.API.Authorization;
using SalonBooking.Application.Features.Feedback;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Security;

namespace SalonBooking.API.Controllers;

[Authorize]
[ApiController]
[Route("api/CustomerFeedback")]
public class CustomerFeedbackController : ControllerBase
{
    private readonly IFeedbackService _feedback;

    public CustomerFeedbackController(IFeedbackService feedback)
    {
        _feedback = feedback;
    }

    [HttpGet("pending")]
    [RequiresPermission(PermissionCatalog.CustomerAppointmentsRead)]
    public async Task<IActionResult> Pending(CancellationToken cancellationToken)
    {
        return Ok(await _feedback.ListPendingForCurrentCustomerAsync(cancellationToken));
    }

    [HttpPost]
    [RequiresPermission(PermissionCatalog.CustomerAppointmentsRead)]
    public async Task<IActionResult> Submit(SubmitFeedbackRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _feedback.SubmitAsync(request, cancellationToken));
    }
}
