using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Auth;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Services.Auth;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class UserAccountRepository : IUserAccountRepository
{
    private readonly PharmacyDbContext _context;

    public UserAccountRepository(PharmacyDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByNormalizedUsernameAsync(string normalizedUsername, CancellationToken cancellationToken = default) =>
        IdentityQuery().FirstOrDefaultAsync(user => user.NormalizedUsername == normalizedUsername, cancellationToken);

    public Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        IdentityQuery().FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public Task<bool> AnyUsersAsync(CancellationToken cancellationToken = default) =>
        _context.Users.AnyAsync(cancellationToken);

    public Task<bool> UsernameExistsAsync(string normalizedUsername, Guid? excludingUserId = null, CancellationToken cancellationToken = default) =>
        _context.Users.AnyAsync(
            user => user.NormalizedUsername == normalizedUsername &&
                (!excludingUserId.HasValue || user.Id != excludingUserId.Value), cancellationToken);

    public Task<bool> EmailExistsAsync(string normalizedEmail, Guid? excludingUserId = null, CancellationToken cancellationToken = default) =>
        _context.Users.AnyAsync(
            user => user.NormalizedEmail == normalizedEmail &&
                (!excludingUserId.HasValue || user.Id != excludingUserId.Value), cancellationToken);

    public Task<Role?> GetRoleByIdAsync(Guid roleId, CancellationToken cancellationToken = default) =>
        RoleQuery().FirstOrDefaultAsync(role => role.Id == roleId, cancellationToken);

    public Task<Role?> GetRoleByNameAsync(string roleName, CancellationToken cancellationToken = default) =>
        RoleQuery().FirstOrDefaultAsync(role => role.Name == roleName, cancellationToken);

    public Task<Branch?> GetBranchByIdAsync(Guid branchId, CancellationToken cancellationToken = default) =>
        _context.Branches.FirstOrDefaultAsync(branch => branch.Id == branchId, cancellationToken);

    public Task<Branch?> GetBranchByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        _context.Branches.FirstOrDefaultAsync(branch => branch.Code == code, cancellationToken);

    public Task<int> CountActiveUsersInRoleAsync(string roleName, CancellationToken cancellationToken = default) =>
        _context.Users.CountAsync(user => user.IsActive && user.Role!.Name == roleName, cancellationToken);

    public async Task<PagedResult<UserListItemDto>> ListUsersAsync(UserListQuery query, CancellationToken cancellationToken = default)
    {
        var users = _context.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            users = users.Where(user =>
                EF.Functions.ILike(user.Username, pattern) ||
                EF.Functions.ILike(user.FullName, pattern) ||
                (user.Email != null && EF.Functions.ILike(user.Email, pattern)) ||
                (user.PhoneNumber != null && EF.Functions.ILike(user.PhoneNumber, pattern)));
        }

        if (query.RoleId.HasValue)
        {
            users = users.Where(user => user.RoleId == query.RoleId.Value);
        }

        if (query.BranchId.HasValue)
        {
            users = users.Where(user => user.BranchId == query.BranchId.Value);
        }

        if (query.IsActive.HasValue)
        {
            users = users.Where(user => user.IsActive == query.IsActive.Value);
        }

        var totalCount = await users.CountAsync(cancellationToken);
        var items = await users
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Username)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(user => new UserListItemDto(
                user.Id,
                user.FullName,
                user.Username,
                new BranchDto(user.BranchId, user.Branch!.Code, user.Branch.Name),
                new RoleDto(user.RoleId, user.Role!.Name, user.Role.Description),
                user.IsActive,
                user.MustChangePassword,
                user.LastLoginAtUtc))
            .ToListAsync(cancellationToken);
        return new PagedResult<UserListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken cancellationToken = default) =>
        await _context.Roles.AsNoTracking()
            .Where(role => role.IsActive)
            .OrderBy(role => role.Name)
            .Select(role => new RoleDto(role.Id, role.Name, role.Description))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PermissionDto>> ListPermissionsAsync(CancellationToken cancellationToken = default) =>
        await _context.Permissions.AsNoTracking()
            .OrderBy(permission => permission.Category)
            .ThenBy(permission => permission.Code)
            .Select(permission => new PermissionDto(permission.Id, permission.Code, permission.Description, permission.Category))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BranchDto>> ListBranchesAsync(CancellationToken cancellationToken = default) =>
        await _context.Branches.AsNoTracking()
            .Where(branch => branch.IsActive)
            .OrderBy(branch => branch.Name)
            .Select(branch => new BranchDto(branch.Id, branch.Code, branch.Name))
            .ToListAsync(cancellationToken);

    public async Task AddRoleAsync(Role role, CancellationToken cancellationToken = default) =>
        await _context.Roles.AddAsync(role, cancellationToken);

    public async Task AddBranchAsync(Branch branch, CancellationToken cancellationToken = default) =>
        await _context.Branches.AddAsync(branch, cancellationToken);

    public async Task AddUserAsync(User user, CancellationToken cancellationToken = default) =>
        await _context.Users.AddAsync(user, cancellationToken);

    public async Task AddAuditAsync(AuditLog auditLog, CancellationToken cancellationToken = default) =>
        await _context.AuditLogs.AddAsync(auditLog, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ResourceConflictException("A user with the same username or email already exists.");
        }
    }

    private IQueryable<User> IdentityQuery() =>
        _context.Users
            .Include(user => user.Role)
            .ThenInclude(role => role!.RolePermissions)
            .ThenInclude(rolePermission => rolePermission.Permission)
            .Include(user => user.Branch);

    private IQueryable<Role> RoleQuery() =>
        _context.Roles
            .Include(role => role.RolePermissions)
            .ThenInclude(rolePermission => rolePermission.Permission);
}
