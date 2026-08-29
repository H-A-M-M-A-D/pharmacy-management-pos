using System.Text.Json;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Auth;
using Pharmacy.Application.Security;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IUserAccountRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly AuthenticationSecurityOptions _securityOptions;
    private readonly TimeProvider _timeProvider;

    public AuthService(
        IUserAccountRepository repository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        AuthenticationSecurityOptions securityOptions,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _securityOptions = securityOptions;
        _timeProvider = timeProvider;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        var user = await _repository.GetByNormalizedUsernameAsync(
            IdentityValidation.NormalizeUsername(request.Username), cancellationToken);
        if (user is null)
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (!user.IsActive || user.LockoutEndUtc > now)
        {
            return null;
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= _securityOptions.MaximumFailedAttempts)
            {
                user.LockoutEndUtc = now.AddMinutes(_securityOptions.LockoutMinutes);
            }

            user.UpdatedAt = now;
            await AddAuditAsync(user.Id, "LoginFailed", user.Id, new
            {
                user.FailedLoginAttempts,
                Locked = user.LockoutEndUtc > now
            }, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
            return null;
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEndUtc = null;
        user.LastLoginAtUtc = now;
        user.UpdatedAt = now;
        await AddAuditAsync(user.Id, "LoginSucceeded", user.Id, new { user.LastLoginAtUtc }, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return CreateLoginResponse(user);
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        MapCurrentUser(await GetRequiredActiveUserAsync(userId, cancellationToken));

    public async Task<LoginResponse> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await GetRequiredActiveUserAsync(userId, cancellationToken);
        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new RequestValidationException("Current password is incorrect.");
        }

        IdentityValidation.ValidatePassword(request.NewPassword);
        if (_passwordHasher.Verify(request.NewPassword, user.PasswordHash))
        {
            throw new RequestValidationException("New password must be different from the current password.");
        }

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.MustChangePassword = false;
        user.FailedLoginAttempts = 0;
        user.LockoutEndUtc = null;
        user.TokenVersion++;
        user.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await AddAuditAsync(user.Id, "PasswordChanged", user.Id, new { user.MustChangePassword }, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return CreateLoginResponse(user);
    }

    public async Task<CurrentUserDto> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await GetRequiredActiveUserAsync(userId, cancellationToken);
        IdentityValidation.ValidateProfile(request.FullName, request.Email, request.PhoneNumber);
        var normalizedEmail = IdentityValidation.NormalizeEmail(request.Email);
        if (normalizedEmail is not null &&
            await _repository.EmailExistsAsync(normalizedEmail, user.Id, cancellationToken))
        {
            throw new ResourceConflictException("Email address is already in use.");
        }

        var oldValues = new { user.FullName, user.Email, user.PhoneNumber };
        user.FullName = request.FullName.Trim();
        user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        user.NormalizedEmail = normalizedEmail;
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        user.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await AddAuditAsync(user.Id, "UserUpdated", user.Id, new
        {
            Old = oldValues,
            New = new { user.FullName, user.Email, user.PhoneNumber }
        }, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return MapCurrentUser(user);
    }

    public async Task<CurrentUserDto> CreateOwnerAsync(
        SetupOwnerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await _repository.AnyUsersAsync(cancellationToken))
        {
            throw new ResourceConflictException("Initial owner setup is unavailable.");
        }

        IdentityValidation.ValidateUsername(request.Username);
        IdentityValidation.ValidateProfile(request.FullName, request.Email, null);
        IdentityValidation.ValidatePassword(request.Password);
        var ownerRole = await _repository.GetRoleByNameAsync(RoleCatalog.Owner, cancellationToken)
            ?? throw new ResourceNotFoundException("Owner role is unavailable. Apply the Phase 2 migration first.");
        var branch = await _repository.GetBranchByCodeAsync("HQ", cancellationToken);
        if (branch is null)
        {
            branch = new Branch { Code = "HQ", Name = "Head Office", IsHeadOffice = true, IsActive = true };
            await _repository.AddBranchAsync(branch, cancellationToken);
        }

        var user = new User
        {
            Username = request.Username.Trim(),
            NormalizedUsername = IdentityValidation.NormalizeUsername(request.Username),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            NormalizedEmail = IdentityValidation.NormalizeEmail(request.Email),
            FullName = request.FullName.Trim(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            BranchId = branch.Id,
            Branch = branch,
            RoleId = ownerRole.Id,
            Role = ownerRole,
            IsActive = true,
            MustChangePassword = false
        };
        await _repository.AddUserAsync(user, cancellationToken);
        await AddAuditAsync(user.Id, "UserCreated", user.Id, new
        {
            user.Username, user.FullName, user.Email, user.BranchId, user.RoleId, user.IsActive
        }, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return MapCurrentUser(user);
    }

    private async Task<User> GetRequiredActiveUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _repository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new ResourceNotFoundException("User was not found.");
        }

        return user;
    }

    private LoginResponse CreateLoginResponse(User user)
    {
        var currentUser = MapCurrentUser(user);
        var token = _tokenService.CreateToken(currentUser, user.TokenVersion);
        return new LoginResponse(token.Token, token.ExpiresAtUtc, currentUser);
    }

    private static CurrentUserDto MapCurrentUser(User user)
    {
        var role = user.Role ?? throw new InvalidOperationException("User role was not loaded.");
        var branch = user.Branch ?? throw new InvalidOperationException("User branch was not loaded.");
        var permissions = role.RolePermissions
            .Where(item => item.Permission is not null)
            .Select(item => item.Permission!.Code)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code)
            .ToArray();
        return new CurrentUserDto(
            user.Id, user.Username, user.FullName, user.Email, user.PhoneNumber,
            new BranchDto(branch.Id, branch.Code, branch.Name),
            [new RoleDto(role.Id, role.Name, role.Description)],
            permissions, user.MustChangePassword);
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
