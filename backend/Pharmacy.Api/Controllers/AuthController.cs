using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Authorization;
using Pharmacy.Application.DTOs.Auth;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Auth;

namespace Pharmacy.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        return result is null ? Unauthorized(new { message = "Invalid username or password." }) : Ok(result);
    }

    [HttpPost("setup-owner")]
    [AllowAnonymous]
    public async Task<ActionResult<CurrentUserDto>> SetupOwner(SetupOwnerRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.CreateOwnerAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("me")]
    [HasPermission(PermissionCatalog.ProfileView)]
    public Task<CurrentUserDto> Me(CancellationToken cancellationToken) =>
        authService.GetCurrentUserAsync(CurrentUserId, cancellationToken);

    [HttpPut("me")]
    [HasPermission(PermissionCatalog.ProfileUpdate)]
    public Task<CurrentUserDto> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken) =>
        authService.UpdateProfileAsync(CurrentUserId, request, cancellationToken);

    [HttpPost("change-password")]
    [HasPermission(PermissionCatalog.ProfileChangePassword)]
    public Task<LoginResponse> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken) =>
        authService.ChangePasswordAsync(CurrentUserId, request, cancellationToken);

    private Guid CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new UnauthorizedAccessException();
}
