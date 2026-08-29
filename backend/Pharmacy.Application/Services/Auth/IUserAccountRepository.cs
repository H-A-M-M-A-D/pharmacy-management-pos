using Pharmacy.Application.DTOs.Auth;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Auth;

public interface IUserAccountRepository
{
    Task<User?> GetByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> AnyUsersAsync(CancellationToken cancellationToken = default);
    Task<bool> UsernameExistsAsync(string normalizedUsername, Guid? excludingUserId = null, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string normalizedEmail, Guid? excludingUserId = null, CancellationToken cancellationToken = default);
    Task<Role?> GetRoleByIdAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<Role?> GetRoleByNameAsync(string roleName, CancellationToken cancellationToken = default);
    Task<Branch?> GetBranchByIdAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<Branch?> GetBranchByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<int> CountActiveUsersInRoleAsync(string roleName, CancellationToken cancellationToken = default);
    Task<PagedResult<UserListItemDto>> ListUsersAsync(UserListQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PermissionDto>> ListPermissionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BranchDto>> ListBranchesAsync(CancellationToken cancellationToken = default);
    Task AddRoleAsync(Role role, CancellationToken cancellationToken = default);
    Task AddBranchAsync(Branch branch, CancellationToken cancellationToken = default);
    Task AddUserAsync(User user, CancellationToken cancellationToken = default);
    Task AddAuditAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
