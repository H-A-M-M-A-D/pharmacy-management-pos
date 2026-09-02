using Pharmacy.Application.DTOs.Inventory;
using Pharmacy.Application.DTOs.Users;

namespace Pharmacy.Application.Services.Inventory;

public interface IInventoryService
{
    Task<PagedResult<InventoryListItemDto>> ListInventoryAsync(Guid actorId, InventoryListQuery query, CancellationToken cancellationToken = default);
    Task<InventoryDetailsDto> GetInventoryDetailsAsync(Guid actorId, Guid branchId, Guid productId, CancellationToken cancellationToken = default);
    Task<PagedResult<BatchListItemDto>> ListBatchesAsync(Guid actorId, BatchListQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpiryListItemDto>> ListExpiryAsync(Guid actorId, ExpiryQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<StockMovementListItemDto>> ListMovementsAsync(Guid actorId, StockMovementListQuery query, CancellationToken cancellationToken = default);
    Task<InventoryDetailsDto> AddOpeningStockAsync(Guid actorId, OpeningStockRequest request, CancellationToken cancellationToken = default);
    Task<InventoryDetailsDto> AdjustStockIncreaseAsync(Guid actorId, StockAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task<InventoryDetailsDto> AdjustStockDecreaseAsync(Guid actorId, StockAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task<InventoryDetailsDto> ReconcileStockCountAsync(Guid actorId, StockCountRequest request, CancellationToken cancellationToken = default);
    Task<InventoryDetailsDto> DisposeExpiredStockAsync(Guid actorId, DisposeExpiredStockRequest request, CancellationToken cancellationToken = default);
    Task<FefoPreviewDto> PreviewFefoAsync(Guid actorId, FefoPreviewRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryIntegrityIssueDto>> CheckIntegrityAsync(Guid actorId, Guid? branchId, Guid? productId, CancellationToken cancellationToken = default);
    Task<InventoryOptionsDto> GetOptionsAsync(Guid actorId, string? productSearch, CancellationToken cancellationToken = default);
}
