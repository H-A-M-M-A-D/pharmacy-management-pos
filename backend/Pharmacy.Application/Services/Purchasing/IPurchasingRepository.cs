using System.Data;
using Pharmacy.Application.Common;
using Pharmacy.Application.DTOs.Purchasing;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.Purchasing;

public interface IPurchasingRepository
{
    Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<Supplier?> GetSupplierAsync(Guid supplierId, CancellationToken cancellationToken = default);
    Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<ProductBatch?> GetBatchByNumberAsync(Guid branchId, Guid productId, string batchNumber, CancellationToken cancellationToken = default);
    Task<Pharmacy.Domain.Entities.Inventory?> GetInventoryAsync(Guid branchId, Guid productId, Guid batchId, CancellationToken cancellationToken = default);
    Task<PurchaseOrder?> GetPurchaseOrderAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GoodsReceipt?> GetGoodsReceiptAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SupplierInvoiceExistsAsync(Guid supplierId, string normalizedInvoiceNumber, CancellationToken cancellationToken = default);
    Task<string> NextPurchaseOrderNumberAsync(DateOnly orderDate, CancellationToken cancellationToken = default);
    Task<string> NextGrnNumberAsync(DateOnly receiptDate, CancellationToken cancellationToken = default);
    Task AddPurchaseOrderAsync(PurchaseOrder order, CancellationToken cancellationToken = default);
    Task AddGoodsReceiptAsync(GoodsReceipt receipt, CancellationToken cancellationToken = default);
    Task AddBatchAsync(ProductBatch batch, CancellationToken cancellationToken = default);
    Task AddInventoryAsync(Pharmacy.Domain.Entities.Inventory inventory, CancellationToken cancellationToken = default);
    Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken = default);
    Task AddSupplierLedgerEntryAsync(SupplierLedgerEntry entry, CancellationToken cancellationToken = default);
    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default);
    Task<PagedResult<PurchaseOrderListItemDto>> ListPurchaseOrdersAsync(PurchaseOrderListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task<PurchaseOrderDetailsDto?> GetPurchaseOrderDetailsAsync(Guid id, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task<PagedResult<PurchaseHistoryItemDto>> ListPurchasesAsync(PurchaseHistoryQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task<GoodsReceiptDetailsDto?> GetGoodsReceiptDetailsAsync(Guid id, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task<PurchasingOptionsDto> GetOptionsAsync(string? productSearch, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
