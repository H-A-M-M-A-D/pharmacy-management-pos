using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Godowns;

/// <summary>
/// Narrow, cross-module port used by transaction services (POS, purchasing, inventory) to resolve and
/// authorize the godown a transaction should operate against, without depending on the full Godown
/// master-data repository. Mirrors the role <see cref="Pharmacy.Application.Services.Accounting.IJournalPostingService"/>
/// plays for journal posting.
/// </summary>
public interface IGodownAccessService
{
    Task<Godown?> GetGodownAsync(Guid godownId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the branch's active default godown id, or null if the branch has no godowns configured yet
    /// (legacy/unmigrated data or a fresh install before Godown Master is set up).
    /// </summary>
    Task<Guid?> GetDefaultGodownIdAsync(Guid branchId, CancellationToken cancellationToken = default);

    Task<bool> UserHasAccessAsync(Guid userId, Guid godownId, CancellationToken cancellationToken = default);
}
