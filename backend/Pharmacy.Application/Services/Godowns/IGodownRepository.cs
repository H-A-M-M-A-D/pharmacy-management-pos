using System.Data;
using Pharmacy.Application.DTOs.Godowns;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Godowns;

public interface IGodownRepository
{
    Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<Godown?> GetGodownAsync(Guid godownId, CancellationToken cancellationToken = default);
    Task<bool> NormalizedCodeExistsAsync(Guid branchId, string normalizedCode, Guid? excludingId = null, CancellationToken cancellationToken = default);
    Task<Godown?> GetDefaultGodownAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<int> ActiveGodownCountAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<bool> GodownHasHistoryAsync(Guid godownId, CancellationToken cancellationToken = default);
    Task AddGodownAsync(Godown godown, CancellationToken cancellationToken = default);
    Task ClearDefaultForBranchAsync(Guid branchId, Guid? excludingId = null, CancellationToken cancellationToken = default);
    Task<PagedResult<GodownListItemDto>> ListGodownsAsync(GodownListQuery query, CancellationToken cancellationToken = default);
    Task<GodownDetailsDto?> GetGodownDetailsAsync(Guid godownId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GodownLookupDto>> LookupGodownsAsync(Guid? branchId, bool activeOnly, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GodownLookupDto>> GetAllowedGodownsForUserAsync(Guid userId, Guid branchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserGodown?> GetUserGodownAsync(Guid userId, Guid godownId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserGodownDto>> ListUserGodownsAsync(Guid godownId, CancellationToken cancellationToken = default);
    Task AddUserGodownAsync(UserGodown userGodown, CancellationToken cancellationToken = default);
    Task RemoveUserGodownAsync(UserGodown userGodown, CancellationToken cancellationToken = default);
    Task ClearUserDefaultAsync(Guid userId, Guid? excludingGodownId = null, CancellationToken cancellationToken = default);
    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
