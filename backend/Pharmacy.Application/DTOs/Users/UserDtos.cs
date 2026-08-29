using Pharmacy.Application.DTOs.Auth;

namespace Pharmacy.Application.DTOs.Users;

public sealed record CreateUserRequest(
    string FullName,
    string Username,
    string? Email,
    string? PhoneNumber,
    Guid BranchId,
    Guid RoleId,
    string TemporaryPassword,
    bool IsActive = true);

public sealed record UpdateUserRequest(
    string FullName,
    string? Email,
    string? PhoneNumber,
    Guid BranchId,
    Guid RoleId);

public sealed record ResetPasswordRequest(string TemporaryPassword);

public sealed record UserListQuery(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    Guid? RoleId = null,
    Guid? BranchId = null,
    bool? IsActive = null);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record UserListItemDto(
    Guid Id,
    string FullName,
    string Username,
    BranchDto Branch,
    RoleDto Role,
    bool IsActive,
    bool MustChangePassword,
    DateTime? LastLoginAtUtc);

public sealed record UserDetailsDto(
    Guid Id,
    string FullName,
    string Username,
    string? Email,
    string? PhoneNumber,
    BranchDto Branch,
    IReadOnlyList<RoleDto> Roles,
    bool IsActive,
    bool MustChangePassword,
    DateTime? LastLoginAtUtc,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record UserManagementOptionsDto(
    IReadOnlyList<BranchDto> Branches,
    IReadOnlyList<RoleDto> Roles);
