using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Users;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(IUserManagementService users) : ControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCatalog.UsersView)]
    public Task<PagedResult<UserListItemDto>> List([FromQuery] UserListQuery query, CancellationToken cancellationToken) =>
        users.ListAsync(query, cancellationToken);

    [HttpGet("options")]
    [HasPermission(PermissionCatalog.UsersView)]
    public Task<UserManagementOptionsDto> Options(CancellationToken cancellationToken) =>
        users.GetOptionsAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionCatalog.UsersView)]
    public Task<UserDetailsDto> Get(Guid id, CancellationToken cancellationToken) =>
        users.GetAsync(id, cancellationToken);

    [HttpPost]
    [HasPermission(PermissionCatalog.UsersCreate)]
    public async Task<ActionResult<UserDetailsDto>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await users.CreateAsync(CurrentUserId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionCatalog.UsersUpdate)]
    public Task<UserDetailsDto> Update(Guid id, UpdateUserRequest request, CancellationToken cancellationToken) =>
        users.UpdateAsync(CurrentUserId, id, request, cancellationToken);

    [HttpPost("{id:guid}/activate")]
    [HasPermission(PermissionCatalog.UsersActivate)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        await users.ActivateAsync(CurrentUserId, id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/deactivate")]
    [HasPermission(PermissionCatalog.UsersDeactivate)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await users.DeactivateAsync(CurrentUserId, id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/reset-password")]
    [HasPermission(PermissionCatalog.UsersResetPassword)]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await users.ResetPasswordAsync(CurrentUserId, id, request, cancellationToken);
        return NoContent();
    }

    private Guid CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new UnauthorizedAccessException();
}
