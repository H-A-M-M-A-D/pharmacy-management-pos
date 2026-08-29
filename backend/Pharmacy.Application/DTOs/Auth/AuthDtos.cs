namespace Pharmacy.Application.DTOs.Auth;

/// <summary>
/// DTO for login request.
/// </summary>
public class LoginRequest
{
    public required string Username { get; set; }
    public required string Password { get; set; }
}

/// <summary>
/// DTO for login response.
/// </summary>
public class LoginResponse
{
    public required string Token { get; set; }
    public required UserDto User { get; set; }
}

/// <summary>
/// User DTO for API responses.
/// </summary>
public class UserDto
{
    public Guid Id { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string FullName { get; set; }
    public required string RoleName { get; set; }
    public Guid RoleId { get; set; }
}
