using Microsoft.AspNetCore.Mvc;
using SalonBooking.API.Authorization;
using SalonBooking.Application.Features.Identity;
using SalonBooking.Application.Interfaces;
using SalonBooking.Application.Security;

namespace SalonBooking.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserAccountService _users;

    public UsersController(IUserAccountService users)
    {
        _users = users;
    }

    [HttpGet]
    [RequiresPermission(PermissionCatalog.UsersRead)]
    public async Task<IActionResult> Get([FromQuery] UserQueryRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _users.GetUsersAsync(request, cancellationToken));
    }

    [HttpGet("{id:long}")]
    [RequiresPermission(PermissionCatalog.UsersRead)]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        return Ok(await _users.GetUserAsync(id, cancellationToken));
    }

    [HttpPost]
    [RequiresPermission(PermissionCatalog.UsersManage)]
    public async Task<IActionResult> Create(SaveUserRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _users.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:long}")]
    [RequiresPermission(PermissionCatalog.UsersManage)]
    public async Task<IActionResult> Update(long id, SaveUserRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _users.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:long}/activation")]
    [RequiresPermission(PermissionCatalog.UsersManage)]
    public async Task<IActionResult> SetActive(
        long id,
        SetUserActiveRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _users.SetActiveAsync(id, request.IsActive, cancellationToken));
    }

    [HttpPut("{id:long}/roles")]
    [RequiresPermission(PermissionCatalog.UsersManage)]
    public async Task<IActionResult> SetRoles(
        long id,
        SetUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _users.SetRolesAsync(id, request.RoleIds, cancellationToken));
    }
}
