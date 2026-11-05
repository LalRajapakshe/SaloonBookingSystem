using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalonBooking.API.Authorization;
using SalonBooking.Application.Features.Qualifications;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Security;

namespace SalonBooking.API.Controllers;

[Authorize]
[ApiController]
[Route("api/Qualifications")]
public class QualificationsController : ControllerBase
{
    private readonly IQualificationAdminService _qualifications;

    public QualificationsController(IQualificationAdminService qualifications)
    {
        _qualifications = qualifications;
    }

    [HttpGet]
    [RequiresPermission(PermissionCatalog.QualificationsRead)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        return Ok(await _qualifications.ListAsync(cancellationToken));
    }

    [HttpPost]
    [RequiresPermission(PermissionCatalog.QualificationsManage)]
    public async Task<IActionResult> Create(SaveQualificationRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _qualifications.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:long}")]
    [RequiresPermission(PermissionCatalog.QualificationsManage)]
    public async Task<IActionResult> Update(long id, SaveQualificationRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _qualifications.UpdateAsync(id, request, cancellationToken));
    }

    [HttpGet("employees/{employeeId:long}")]
    [RequiresPermission(PermissionCatalog.QualificationsRead)]
    public async Task<IActionResult> ForEmployee(long employeeId, CancellationToken cancellationToken)
    {
        return Ok(await _qualifications.ListForEmployeeAsync(employeeId, cancellationToken));
    }

    [HttpPost("employees")]
    [RequiresPermission(PermissionCatalog.QualificationsManage)]
    public async Task<IActionResult> SaveForEmployee(SaveEmployeeQualificationRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _qualifications.SaveForEmployeeAsync(request, cancellationToken));
    }

    [HttpPut("employees/{id:long}")]
    [RequiresPermission(PermissionCatalog.QualificationsManage)]
    public async Task<IActionResult> UpdateForEmployee(long id, SaveEmployeeQualificationRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _qualifications.UpdateForEmployeeAsync(id, request, cancellationToken));
    }

    [HttpDelete("employees/{id:long}")]
    [RequiresPermission(PermissionCatalog.QualificationsManage)]
    public async Task<IActionResult> DeleteForEmployee(long id, CancellationToken cancellationToken)
    {
        await _qualifications.DeleteForEmployeeAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("employees/{id:long}/certificate")]
    [RequiresPermission(PermissionCatalog.QualificationsManage)]
    [RequestSizeLimit(5_242_880)]
    public async Task<IActionResult> SaveCertificate(long id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { title = "A certificate file is required." });
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        await _qualifications.SaveCertificateAsync(id, buffer.ToArray(), cancellationToken);
        return NoContent();
    }

    [HttpGet("employees/{id:long}/certificate")]
    [RequiresPermission(PermissionCatalog.QualificationsRead)]
    public async Task<IActionResult> Certificate(long id, CancellationToken cancellationToken)
    {
        var file = await _qualifications.OpenCertificateAsync(id, cancellationToken);
        return file == null ? NotFound() : File(file.Content, file.ContentType);
    }

    [HttpDelete("employees/{id:long}/certificate")]
    [RequiresPermission(PermissionCatalog.QualificationsManage)]
    public async Task<IActionResult> RemoveCertificate(long id, CancellationToken cancellationToken)
    {
        await _qualifications.RemoveCertificateAsync(id, cancellationToken);
        return NoContent();
    }
}
