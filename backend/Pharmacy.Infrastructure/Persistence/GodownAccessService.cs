using Microsoft.EntityFrameworkCore;
using Pharmacy.Application.Services.Godowns;
using Pharmacy.Domain.Entities;
using Pharmacy.Infrastructure.Data;

namespace Pharmacy.Infrastructure.Persistence;

public sealed class GodownAccessService(PharmacyDbContext context) : IGodownAccessService
{
    public Task<Godown?> GetGodownAsync(Guid godownId, CancellationToken cancellationToken = default) =>
        context.Godowns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == godownId, cancellationToken);

    public async Task<Guid?> GetDefaultGodownIdAsync(Guid branchId, CancellationToken cancellationToken = default) =>
        await context.Godowns.AsNoTracking().Where(x => x.BranchId == branchId && x.IsDefault && x.IsActive)
            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);

    public Task<bool> UserHasAccessAsync(Guid userId, Guid godownId, CancellationToken cancellationToken = default) =>
        context.UserGodowns.AsNoTracking().AnyAsync(x => x.UserId == userId && x.GodownId == godownId, cancellationToken);
}
