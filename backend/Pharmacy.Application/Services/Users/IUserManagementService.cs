using Pharmacy.Application.DTOs.Auth;
using Pharmacy.Application.DTOs.Users;

namespace Pharmacy.Application.Services.Users;

public interface IUserManagementService
{
    Task<PagedResult<UserListItemDto>> ListAsync(UserListQuery query, CancellationToken cancellationToken = default);
    Task<UserDetailsDto> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserDetailsDto> CreateAsync(Guid actorUserId, CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<UserDetailsDto> UpdateAsync(Guid actorUserId, Guid userId, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task ActivateAsync(Guid actorUserId, Guid userId, CancellationToken cancellationToken = default);
    Task DeactivateAsync(Guid actorUserId, Guid userId, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(Guid actorUserId, Guid userId, ResetPasswordRequest request, CancellationToken cancellationToken = default);
    Task<UserManagementOptionsDto> GetOptionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PermissionDto>> ListPermissionsAsync(CancellationToken cancellationToken = default);
}
