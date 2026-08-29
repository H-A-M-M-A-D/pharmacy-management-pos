using Pharmacy.Application.DTOs.Auth;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Auth;

/// <summary>
/// Authentication service implementation.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public AuthService(IUserAccountRepository userAccountRepository, IPasswordHasher passwordHasher, ITokenService tokenService)
    {
        _userAccountRepository = userAccountRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userAccountRepository.GetActiveUserByUsernameAsync(request.Username, cancellationToken);
        if (user == null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return null;
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userAccountRepository.SaveChangesAsync(cancellationToken);

        var permissionCodes = user.Role?.RolePermissions
            .Where(rp => rp.Permission != null)
            .Select(rp => rp.Permission!.Code)
            .ToArray() ?? Array.Empty<string>();

        var token = _tokenService.CreateToken(
            user.Id,
            user.Username,
            user.Email,
            user.FullName,
            user.Role?.Name ?? "Unknown",
            user.RoleId,
            user.BranchId,
            permissionCodes);

        return new LoginResponse
        {
            Token = token,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FullName = user.FullName,
                RoleName = user.Role?.Name ?? "Unknown",
                RoleId = user.RoleId
            }
        };
    }

    public async Task<string> GenerateTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userAccountRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found");

        var permissionCodes = user.Role?.RolePermissions
            .Where(rp => rp.Permission != null)
            .Select(rp => rp.Permission!.Code)
            .ToArray() ?? Array.Empty<string>();

        return _tokenService.CreateToken(
            user.Id,
            user.Username,
            user.Email,
            user.FullName,
            user.Role?.Name ?? "Unknown",
            user.RoleId,
            user.BranchId,
            permissionCodes);
    }

    public async Task<UserDto> CreateOwnerAsync(string username, string email, string fullName, string password, CancellationToken cancellationToken = default)
    {
        if (await _userAccountRepository.AnyUsersAsync(cancellationToken))
        {
            throw new InvalidOperationException("Initial owner setup is unavailable after the first user has been created.");
        }

        var ownerRole = await _userAccountRepository.GetRoleByNameAsync("Owner", cancellationToken);
        if (ownerRole == null)
        {
            ownerRole = new Role
            {
                Name = "Owner",
                Description = "System owner with all permissions",
                IsSystem = true,
                IsActive = true
            };
            await _userAccountRepository.AddRoleAsync(ownerRole, cancellationToken);
        }

        var branch = await _userAccountRepository.GetBranchByCodeAsync("HQ", cancellationToken);
        if (branch == null)
        {
            branch = new Branch
            {
                Code = "HQ",
                Name = "Head Office",
                IsHeadOffice = true,
                IsActive = true
            };
            await _userAccountRepository.AddBranchAsync(branch, cancellationToken);
        }

        var user = new User
        {
            Username = username,
            Email = email,
            FullName = fullName,
            PasswordHash = _passwordHasher.Hash(password),
            BranchId = branch.Id,
            RoleId = ownerRole.Id,
            IsActive = true
        };

        await _userAccountRepository.AddAsync(user, cancellationToken);
        await _userAccountRepository.SaveChangesAsync(cancellationToken);

        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            RoleName = ownerRole.Name,
            RoleId = ownerRole.Id
        };
    }
}
