using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Godowns;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Application.Services.Godowns;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class GodownRepository(PharmacyDbContext context) : IGodownRepository
{
    public Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        context.Users.Include(x => x.Role).ThenInclude(x => x!.RolePermissions).ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == actorId, cancellationToken);

    public Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default) =>
        context.Branches.FirstOrDefaultAsync(x => x.Id == branchId, cancellationToken);

    public Task<Godown?> GetGodownAsync(Guid godownId, CancellationToken cancellationToken = default) =>
        context.Godowns.FirstOrDefaultAsync(x => x.Id == godownId, cancellationToken);

    public Task<bool> NormalizedCodeExistsAsync(Guid branchId, string normalizedCode, Guid? excludingId = null, CancellationToken cancellationToken = default) =>
        context.Godowns.AnyAsync(x => x.BranchId == branchId && x.NormalizedCode == normalizedCode && (!excludingId.HasValue || x.Id != excludingId), cancellationToken);

    public Task<Godown?> GetDefaultGodownAsync(Guid branchId, CancellationToken cancellationToken = default) =>
        context.Godowns.FirstOrDefaultAsync(x => x.BranchId == branchId && x.IsDefault && x.IsActive, cancellationToken);

    public Task<int> ActiveGodownCountAsync(Guid branchId, CancellationToken cancellationToken = default) =>
        context.Godowns.CountAsync(x => x.BranchId == branchId && x.IsActive, cancellationToken);

    public async Task<bool> GodownHasHistoryAsync(Guid godownId, CancellationToken cancellationToken = default) =>
        await context.StockMovements.AnyAsync(x => x.GodownId == godownId, cancellationToken) ||
        await context.ProductBatches.AnyAsync(x => x.GodownId == godownId, cancellationToken);

    public async Task AddGodownAsync(Godown godown, CancellationToken cancellationToken = default) => await context.Godowns.AddAsync(godown, cancellationToken);

    public async Task ClearDefaultForBranchAsync(Guid branchId, Guid? excludingId = null, CancellationToken cancellationToken = default)
    {
        var current = await context.Godowns.Where(x => x.BranchId == branchId && x.IsDefault && (!excludingId.HasValue || x.Id != excludingId)).ToListAsync(cancellationToken);
        foreach (var godown in current) godown.IsDefault = false;
    }

    public async Task<PagedResult<GodownListItemDto>> ListGodownsAsync(GodownListQuery query, CancellationToken cancellationToken = default)
    {
        var godowns = context.Godowns.AsNoTracking().Include(x => x.Branch).AsQueryable();
        if (query.BranchId.HasValue) godowns = godowns.Where(x => x.BranchId == query.BranchId);
        if (query.IsActive.HasValue) godowns = godowns.Where(x => x.IsActive == query.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            godowns = godowns.Where(x => EF.Functions.ILike(x.Name, pattern) || EF.Functions.ILike(x.Code, pattern));
        }
        var total = await godowns.CountAsync(cancellationToken);
        var items = await godowns.OrderBy(x => x.Branch!.Name).ThenBy(x => x.Name)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new GodownListItemDto(x.Id, x.BranchId, x.Branch!.Name, x.Code, x.Name, x.Description, x.IsDefault, x.IsActive, x.CreatedAt))
            .ToListAsync(cancellationToken);
        return new(items, query.Page, query.PageSize, total);
    }

    public async Task<GodownDetailsDto?> GetGodownDetailsAsync(Guid godownId, CancellationToken cancellationToken = default) =>
        await context.Godowns.AsNoTracking().Include(x => x.Branch)
            .Where(x => x.Id == godownId)
            .Select(x => new GodownDetailsDto(x.Id, x.BranchId, x.Branch!.Name, x.Code, x.Name, x.Description, x.IsDefault, x.IsActive, x.CreatedAt, x.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<GodownLookupDto>> LookupGodownsAsync(Guid? branchId, bool activeOnly, CancellationToken cancellationToken = default)
    {
        var godowns = context.Godowns.AsNoTracking().AsQueryable();
        if (branchId.HasValue) godowns = godowns.Where(x => x.BranchId == branchId);
        if (activeOnly) godowns = godowns.Where(x => x.IsActive);
        return await godowns.OrderBy(x => x.Name).Select(x => new GodownLookupDto(x.Id, x.BranchId, x.Code, x.Name, x.IsDefault, x.IsActive)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GodownLookupDto>> GetAllowedGodownsForUserAsync(Guid userId, Guid branchId, bool canSelectBranch, CancellationToken cancellationToken = default)
    {
        var activeGodowns = context.Godowns.AsNoTracking().Where(x => x.BranchId == branchId && x.IsActive);
        if (canSelectBranch)
        {
            return await activeGodowns.OrderBy(x => x.Name).Select(x => new GodownLookupDto(x.Id, x.BranchId, x.Code, x.Name, x.IsDefault, x.IsActive)).ToListAsync(cancellationToken);
        }
        var allowedIds = await context.UserGodowns.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.GodownId).ToListAsync(cancellationToken);
        return await activeGodowns.Where(x => allowedIds.Contains(x.Id)).OrderBy(x => x.Name)
            .Select(x => new GodownLookupDto(x.Id, x.BranchId, x.Code, x.Name, x.IsDefault, x.IsActive)).ToListAsync(cancellationToken);
    }

    public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        context.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

    public Task<UserGodown?> GetUserGodownAsync(Guid userId, Guid godownId, CancellationToken cancellationToken = default) =>
        context.UserGodowns.FirstOrDefaultAsync(x => x.UserId == userId && x.GodownId == godownId, cancellationToken);

    public async Task<IReadOnlyList<UserGodownDto>> ListUserGodownsAsync(Guid godownId, CancellationToken cancellationToken = default) =>
        await context.UserGodowns.AsNoTracking().Include(x => x.User).Where(x => x.GodownId == godownId)
            .Select(x => new UserGodownDto(x.UserId, x.User!.FullName, x.GodownId, x.Godown!.Name, x.IsDefault))
            .ToListAsync(cancellationToken);

    public async Task AddUserGodownAsync(UserGodown userGodown, CancellationToken cancellationToken = default) => await context.UserGodowns.AddAsync(userGodown, cancellationToken);

    public Task RemoveUserGodownAsync(UserGodown userGodown, CancellationToken cancellationToken = default)
    {
        context.UserGodowns.Remove(userGodown);
        return Task.CompletedTask;
    }

    public async Task ClearUserDefaultAsync(Guid userId, Guid? excludingGodownId = null, CancellationToken cancellationToken = default)
    {
        var current = await context.UserGodowns.Where(x => x.UserId == userId && x.IsDefault && (!excludingGodownId.HasValue || x.GodownId != excludingGodownId)).ToListAsync(cancellationToken);
        foreach (var mapping in current) mapping.IsDefault = false;
    }

    public async Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default) => await context.AuditLogs.AddAsync(audit, cancellationToken);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
    {
        await using var tx = await context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        try { await operation(cancellationToken); await tx.CommitAsync(cancellationToken); }
        catch { await tx.RollbackAsync(cancellationToken); throw; }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ResourceConflictException("A godown with this code, or a default godown, already exists for this branch.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
        {
            throw new ResourceConflictException("This godown record changed while saving. Refresh and try again.");
        }
    }
}
