namespace Pharmacy.Application.DTOs.Auth;

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, CurrentUserDto User);

public sealed record CurrentUserDto(
    Guid Id,
    string Username,
    string FullName,
    string? Email,
    string? PhoneNumber,
    BranchDto Branch,
    IReadOnlyList<RoleDto> Roles,
    IReadOnlyList<string> Permissions,
    bool MustChangePassword);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record UpdateProfileRequest(string FullName, string? Email, string? PhoneNumber);

public sealed record SetupOwnerRequest(string Username, string? Email, string FullName, string Password);

public sealed record AccessTokenResult(string Token, DateTime ExpiresAtUtc);

public sealed record BranchDto(Guid Id, string Code, string Name);

public sealed record RoleDto(Guid Id, string Name, string? Description);

public sealed record PermissionDto(Guid Id, string Code, string Description, string Category);
