using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Pharmacy.Application.DTOs.Auth;
using Pharmacy.Application.Services.Auth;

namespace Pharmacy.Api.Controllers;

/// <summary>
/// Authentication endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Login with username and password.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Username and password are required");
        }

        var result = await _authService.LoginAsync(request, cancellationToken);
        if (result == null)
        {
            return Unauthorized("Invalid username or password");
        }

        return Ok(result);
    }

    /// <summary>
    /// Create initial Owner user (for first-time setup).
    /// Should be restricted in production.
    /// </summary>
    [HttpPost("setup-owner")]
    [AllowAnonymous]
    public async Task<ActionResult<UserDto>> SetupOwner([FromBody] SetupOwnerRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("All fields are required");
        }

        try
        {
            var user = await _authService.CreateOwnerAsync(
                request.Username,
                request.Email,
                request.FullName,
                request.Password,
                cancellationToken);

            return Ok(user);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Test endpoint to verify JWT is working.
    /// </summary>
    [HttpGet("verify")]
    [Authorize]
    public ActionResult<object> Verify()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

        return Ok(new
        {
            message = "Token is valid",
            userId,
            username
        });
    }
}

/// <summary>
/// DTO for owner setup request.
/// </summary>
public class SetupOwnerRequest
{
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string FullName { get; set; }
    public required string Password { get; set; }
}
