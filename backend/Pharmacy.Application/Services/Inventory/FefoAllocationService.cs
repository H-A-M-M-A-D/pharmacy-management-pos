using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Inventory;

public sealed class FefoAllocationResult
{
    public required Guid BatchId { get; init; }
    public required string BatchNumber { get; init; }
    public required int AllocatedQuantity { get; init; }
    public required DateOnly ExpiryDate { get; init; }
}

public interface IFefoAllocationService
{
    IReadOnlyList<FefoAllocationResult> Allocate(
        IEnumerable<ProductBatch> batches,
        Guid productId,
        Guid branchId,
        int requestedQuantity,
        DateOnly saleDate,
        CancellationToken cancellationToken = default);
}

public sealed class FefoAllocationService : IFefoAllocationService
{
    public IReadOnlyList<FefoAllocationResult> Allocate(
        IEnumerable<ProductBatch> batches,
        Guid productId,
        Guid branchId,
        int requestedQuantity,
        DateOnly saleDate,
        CancellationToken cancellationToken = default)
    {
        if (requestedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedQuantity));
        }

        var remaining = requestedQuantity;
        var results = new List<FefoAllocationResult>();

        var eligible = batches
            .Where(b => b.ProductId == productId)
            .Where(b => b.BranchId == branchId)
            .Where(b => b.QuantityAvailable > 0)
            .Where(b => !b.IsDisposed)
            .Where(b => b.ExpiryDate >= saleDate)
            .OrderBy(b => b.ExpiryDate)
            .ThenBy(b => b.CreatedAt)
            .ThenBy(b => b.BatchNumber)
            .ThenBy(b => b.Id)
            .ToList();

        foreach (var batch in eligible)
        {
            if (remaining <= 0)
            {
                break;
            }

            var alloc = Math.Min(batch.QuantityAvailable, remaining);
            if (alloc <= 0)
            {
                continue;
            }

            results.Add(new FefoAllocationResult
            {
                BatchId = batch.Id,
                BatchNumber = batch.BatchNumber,
                AllocatedQuantity = alloc,
                ExpiryDate = batch.ExpiryDate
            });

            remaining -= alloc;
        }

        if (remaining > 0)
        {
            throw new InvalidOperationException("Insufficient eligible stock for the requested quantity.");
        }

        return results;
    }
}
