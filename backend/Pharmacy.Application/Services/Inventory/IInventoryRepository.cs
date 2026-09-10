using System.Data;
using Pharmacy.Application.DTOs.Inventory;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Inventory;

public interface IInventoryRepository
{
    Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<ProductBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<ProductBatch?> GetBatchByNumberAsync(Guid branchId, Guid? godownId, Guid productId, string batchNumber, CancellationToken cancellationToken = default);
    Task<Pharmacy.Domain.Entities.Inventory?> GetInventoryAsync(Guid branchId, Guid productId, Guid batchId, CancellationToken cancellationToken = default);
    Task<Supplier?> GetSupplierAsync(Guid supplierId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductBatch>> GetEligibleBatchesAsync(Guid branchId, Guid productId, CancellationToken cancellationToken = default);
    Task AddBatchAsync(ProductBatch batch, CancellationToken cancellationToken = default);
    Task AddInventoryAsync(Pharmacy.Domain.Entities.Inventory inventory, CancellationToken cancellationToken = default);
    Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken = default);
    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default);
    Task<PagedResult<InventoryListItemDto>> ListInventoryAsync(InventoryListQuery query, Guid? actorBranchId, bool canSelectBranch, DateOnly businessDate, CancellationToken cancellationToken = default);
    Task<InventoryDetailsDto?> GetInventoryDetailsAsync(Guid branchId, Guid productId, DateOnly businessDate, CancellationToken cancellationToken = default);
    Task<PagedResult<BatchListItemDto>> ListBatchesAsync(BatchListQuery query, Guid? actorBranchId, bool canSelectBranch, DateOnly businessDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpiryListItemDto>> ListExpiryAsync(ExpiryQuery query, Guid? actorBranchId, bool canSelectBranch, DateOnly businessDate, CancellationToken cancellationToken = default);
    Task<PagedResult<StockMovementListItemDto>> ListMovementsAsync(StockMovementListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryIntegrityIssueDto>> CheckIntegrityAsync(Guid? branchId, Guid? productId, CancellationToken cancellationToken = default);
    Task<InventoryOptionsDto> GetOptionsAsync(string? productSearch, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<string> NextStockCountNumberAsync(DateOnly countDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductBatch>> GetEligibleBatchesForCountAsync(Guid branchId, StockCountScope scope, Guid? categoryId,
        IReadOnlyList<Guid>? productIds, IReadOnlyList<Guid>? productBatchIds, CancellationToken cancellationToken = default);
    Task AddStockCountSessionAsync(StockCountSession session, CancellationToken cancellationToken = default);
    Task<StockCountSession?> GetStockCountSessionForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StockCountSessionDto?> GetStockCountSessionDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<StockCountSessionListItemDto>> ListStockCountSessionsAsync(StockCountSessionListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
}
