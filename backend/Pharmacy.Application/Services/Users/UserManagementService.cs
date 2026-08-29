using System.Text.Json;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Auth;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Security;
using Pharmacy.Application.Services.Auth;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Users;

public sealed class UserManagementService : IUserManagementService
{
    private readonly IUserAccountRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TimeProvider _timeProvider;

    public UserManagementService(IUserAccountRepository repository, IPasswordHasher passwordHasher, TimeProvider timeProvider)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _timeProvider = timeProvider;
    }

    public Task<PagedResult<UserListItemDto>> ListAsync(UserListQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100)
        {
            throw new RequestValidationException("Page must be positive and page size must be between 1 and 100.");
        }

        return _repository.ListUsersAsync(query, cancellationToken);
    }

    public async Task<UserDetailsDto> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
        MapDetails(await GetRequiredUserAsync(userId, cancellationToken));

    public async Task<UserDetailsDto> CreateAsync(Guid actorUserId, CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await RequirePermissionAsync(actorUserId, PermissionCatalog.UsersCreate, cancellationToken);
        IdentityValidation.ValidateUsername(request.Username);
        IdentityValidation.ValidateProfile(request.FullName, request.Email, request.PhoneNumber);
        IdentityValidation.ValidatePassword(request.TemporaryPassword);
        var normalizedUsername = IdentityValidation.NormalizeUsername(request.Username);
        if (await _repository.UsernameExistsAsync(normalizedUsername, null, cancellationToken))
        {
            throw new ResourceConflictException("Username is already in use.");
        }

        var normalizedEmail = IdentityValidation.NormalizeEmail(request.Email);
        if (normalizedEmail is not null && await _repository.EmailExistsAsync(normalizedEmail, null, cancellationToken))
        {
            throw new ResourceConflictException("Email address is already in use.");
        }

        var branch = await _repository.GetBranchByIdAsync(request.BranchId, cancellationToken);
        var role = await _repository.GetRoleByIdAsync(request.RoleId, cancellationToken);
        if (branch is null || !branch.IsActive || role is null || !role.IsActive)
        {
            throw new RequestValidationException("Role or branch is invalid or inactive.");
        }

        EnsureOwnerPermission(actor, role);
        var user = new User
        {
            FullName = request.FullName.Trim(),
            Username = request.Username.Trim(),
            NormalizedUsername = normalizedUsername,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            PasswordHash = _passwordHasher.Hash(request.TemporaryPassword),
            BranchId = branch.Id,
            Branch = branch,
            RoleId = role.Id,
            Role = role,
            IsActive = request.IsActive,
            MustChangePassword = true
        };
        await _repository.AddUserAsync(user, cancellationToken);
        await AddAuditAsync(actorUserId, "UserCreated", user.Id, new
        {
            user.FullName, user.Username, user.Email, user.PhoneNumber,
            user.BranchId, user.RoleId, user.IsActive, user.MustChangePassword
        }, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return MapDetails(user);
    }

    public async Task<UserDetailsDto> UpdateAsync(Guid actorUserId, Guid userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await RequirePermissionAsync(actorUserId, PermissionCatalog.UsersUpdate, cancellationToken);
        var user = await GetRequiredUserAsync(userId, cancellationToken);
        var role = await _repository.GetRoleByIdAsync(request.RoleId, cancellationToken);
        var branch = await _repository.GetBranchByIdAsync(request.BranchId, cancellationToken);
        if (role is null || !role.IsActive || branch is null || !branch.IsActive)
        {
            throw new RequestValidationException("Role or branch is invalid or inactive.");
        }

        EnsureOwnerPermission(actor, user.Role, role);
        if (user.Role?.Name == RoleCatalog.Owner && role.Name != RoleCatalog.Owner)
        {
            await EnsureNotLastActiveOwnerAsync(user, cancellationToken);
        }

        IdentityValidation.ValidateProfile(request.FullName, request.Email, request.PhoneNumber);
        var normalizedEmail = IdentityValidation.NormalizeEmail(request.Email);
        if (normalizedEmail is not null && await _repository.EmailExistsAsync(normalizedEmail, user.Id, cancellationToken))
        {
            throw new ResourceConflictException("Email address is already in use.");
        }

        var oldValues = new { user.FullName, user.Email, user.PhoneNumber, user.BranchId, user.RoleId };
        var roleChanged = user.RoleId != role.Id;
        if (roleChanged && actor.Role?.RolePermissions.Any(
                item => item.Permission?.Code == PermissionCatalog.RolesManage) != true)
        {
            throw new ForbiddenOperationException("Changing a user's role requires role-management permission.");
        }

        user.FullName = request.FullName.Trim();
        user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        user.NormalizedEmail = normalizedEmail;
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        user.BranchId = branch.Id;
        user.Branch = branch;
        user.RoleId = role.Id;
        user.Role = role;
        user.TokenVersion++;
        user.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await AddAuditAsync(actorUserId, "UserUpdated", user.Id, new
        {
            Old = oldValues,
            New = new { user.FullName, user.Email, user.PhoneNumber, user.BranchId, user.RoleId }
        }, cancellationToken);
        if (roleChanged)
        {
            await AddAuditAsync(actorUserId, "RolesChanged", user.Id, new { OldRoleId = oldValues.RoleId, NewRoleId = role.Id }, cancellationToken);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return MapDetails(user);
    }

    public async Task ActivateAsync(Guid actorUserId, Guid userId, CancellationToken cancellationToken = default)
    {
        var actor = await RequirePermissionAsync(actorUserId, PermissionCatalog.UsersActivate, cancellationToken);
        var user = await GetRequiredUserAsync(userId, cancellationToken);
        EnsureOwnerPermission(actor, user.Role);
        if (!user.IsActive)
        {
            user.IsActive = true;
            user.TokenVersion++;
            user.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await AddAuditAsync(actorUserId, "UserActivated", user.Id, new { user.IsActive }, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeactivateAsync(Guid actorUserId, Guid userId, CancellationToken cancellationToken = default)
    {
        var actor = await RequirePermissionAsync(actorUserId, PermissionCatalog.UsersDeactivate, cancellationToken);
        var user = await GetRequiredUserAsync(userId, cancellationToken);
        EnsureOwnerPermission(actor, user.Role);
        if (user.IsActive)
        {
            await EnsureNotLastActiveOwnerAsync(user, cancellationToken);
            user.IsActive = false;
            user.TokenVersion++;
            user.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await AddAuditAsync(actorUserId, "UserDeactivated", user.Id, new { user.IsActive }, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ResetPasswordAsync(Guid actorUserId, Guid userId, ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await RequirePermissionAsync(actorUserId, PermissionCatalog.UsersResetPassword, cancellationToken);
        var user = await GetRequiredUserAsync(userId, cancellationToken);
        EnsureOwnerPermission(actor, user.Role);
        IdentityValidation.ValidatePassword(request.TemporaryPassword);
        user.PasswordHash = _passwordHasher.Hash(request.TemporaryPassword);
        user.MustChangePassword = true;
        user.FailedLoginAttempts = 0;
        user.LockoutEndUtc = null;
        user.TokenVersion++;
        user.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await AddAuditAsync(actorUserId, "PasswordReset", user.Id, new { user.MustChangePassword }, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserManagementOptionsDto> GetOptionsAsync(CancellationToken cancellationToken = default) =>
        new(await _repository.ListBranchesAsync(cancellationToken), await _repository.ListRolesAsync(cancellationToken));

    public Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken cancellationToken = default) =>
        _repository.ListRolesAsync(cancellationToken);

    public Task<IReadOnlyList<PermissionDto>> ListPermissionsAsync(CancellationToken cancellationToken = default) =>
        _repository.ListPermissionsAsync(cancellationToken);

    private async Task<User> RequirePermissionAsync(Guid userId, string permission, CancellationToken cancellationToken)
    {
        var actor = await GetRequiredUserAsync(userId, cancellationToken);
        if (!actor.IsActive || actor.Role?.RolePermissions.Any(item => item.Permission?.Code == permission) != true)
        {
            throw new ForbiddenOperationException("The current user is not permitted to perform this operation.");
        }

        return actor;
    }

    private static void EnsureOwnerPermission(User actor, params Role?[] roles)
    {
        if (roles.Any(role => role?.Name == RoleCatalog.Owner) &&
            actor.Role?.RolePermissions.Any(item => item.Permission?.Code == PermissionCatalog.UsersManageOwner) != true)
        {
            throw new ForbiddenOperationException("Owner accounts require owner-management permission.");
        }
    }

    private async Task EnsureNotLastActiveOwnerAsync(User user, CancellationToken cancellationToken)
    {
        if (user.IsActive && user.Role?.Name == RoleCatalog.Owner &&
            await _repository.CountActiveUsersInRoleAsync(RoleCatalog.Owner, cancellationToken) <= 1)
        {
            throw new ResourceConflictException("The last active Owner cannot be deactivated or lose the Owner role.");
        }
    }

    private async Task<User> GetRequiredUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await _repository.GetByIdAsync(userId, cancellationToken)
        ?? throw new ResourceNotFoundException("User was not found.");

    private static UserDetailsDto MapDetails(User user)
    {
        var role = user.Role ?? throw new InvalidOperationException("User role was not loaded.");
        var branch = user.Branch ?? throw new InvalidOperationException("User branch was not loaded.");
        return new UserDetailsDto(
            user.Id, user.FullName, user.Username, user.Email, user.PhoneNumber,
            new BranchDto(branch.Id, branch.Code, branch.Name),
            [new RoleDto(role.Id, role.Name, role.Description)], user.IsActive,
            user.MustChangePassword, user.LastLoginAtUtc, user.CreatedAt, user.UpdatedAt);
    }

    private Task AddAuditAsync(Guid actorId, string action, Guid entityId, object values, CancellationToken cancellationToken) =>
        _repository.AddAuditAsync(new AuditLog
        {
            UserId = actorId,
            Action = action,
            EntityType = "User",
            EntityId = entityId,
            NewValues = JsonSerializer.Serialize(values)
        }, cancellationToken);
}
