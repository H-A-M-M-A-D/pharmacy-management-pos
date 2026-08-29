using Pharmacy.Application.DTOs.Auth;

namespace Pharmacy.Application.Services.Auth;

/// <summary>
/// Interface for authentication operations.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Authenticate user with username and password.
    /// </summary>
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate JWT token for authenticated user.
    /// </summary>
    Task<string> GenerateTokenAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new Owner user (for initial setup).
    /// </summary>
    Task<UserDto> CreateOwnerAsync(string username, string email, string fullName, string password, CancellationToken cancellationToken = default);
}
