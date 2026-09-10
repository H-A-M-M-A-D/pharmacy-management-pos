using System.Data;
using Pharmacy.Application.DTOs.StockTransfers;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.StockTransfers;

public interface IStockTransferRepository
{
    Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<ProductBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default);

    /// <summary>Reads the batch row with a row-level lock (FOR UPDATE), for dispatch-time availability
    /// checks that must be safe against two concurrent dispatches of the same batch.</summary>
    Task<ProductBatch?> GetBatchForUpdateAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<ProductBatch?> GetBatchByNumberAsync(Guid branchId, Guid? godownId, Guid productId, string batchNumber, CancellationToken cancellationToken = default);
    Task<Pharmacy.Domain.Entities.Inventory?> GetInventoryAsync(Guid branchId, Guid productId, Guid batchId, CancellationToken cancellationToken = default);
    Task AddBatchAsync(ProductBatch batch, CancellationToken cancellationToken = default);
    Task AddInventoryAsync(Pharmacy.Domain.Entities.Inventory inventory, CancellationToken cancellationToken = default);
    Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken = default);
    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default);

    Task<string> NextTransferNumberAsync(DateOnly transferDate, CancellationToken cancellationToken = default);
    Task AddTransferAsync(StockTransfer transfer, CancellationToken cancellationToken = default);
    Task<StockTransfer?> GetTransferForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    void ReplaceTransferItems(StockTransfer transfer, IReadOnlyCollection<StockTransferItem> items);

    Task<StockTransferDetailsDto?> GetTransferDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<StockTransferListItemDto>> ListTransfersAsync(StockTransferListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TransferableBatchDto>> ListTransferableBatchesAsync(Guid branchId, Guid godownId, Guid? productId, string? search, CancellationToken cancellationToken = default);

    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
