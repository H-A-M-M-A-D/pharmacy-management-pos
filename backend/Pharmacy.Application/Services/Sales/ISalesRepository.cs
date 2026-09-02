using System.Data;
using Pharmacy.Application.DTOs.Sales;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Domain.Entities;
using DomainInventory = Pharmacy.Domain.Entities.Inventory;

namespace Pharmacy.Application.Services.Sales;

public interface ISalesRepository
{
    Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductBatch>> GetEligibleBatchesAsync(Guid branchId, Guid productId, CancellationToken cancellationToken = default);
    Task<DomainInventory?> GetInventoryAsync(Guid branchId, Guid productId, Guid batchId, CancellationToken cancellationToken = default);
    Task<Sale?> GetSaleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<string> NextInvoiceNumberAsync(DateTime postedAtUtc, CancellationToken cancellationToken = default);
    Task<string> NextHoldNumberAsync(DateTime createdAtUtc, CancellationToken cancellationToken = default);
    Task AddSaleAsync(Sale sale, CancellationToken cancellationToken = default);
    Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken = default);
    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default);
    Task<PagedResult<SaleListItemDto>> ListSalesAsync(SalesHistoryQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task<PagedResult<SaleListItemDto>> ListHeldSalesAsync(HeldSalesQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task<SaleDetailsDto?> GetSaleDetailsAsync(Guid id, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PosProductDto>> SearchProductsAsync(PosProductSearchQuery query, Guid actorBranchId, bool canSelectBranch, DateOnly businessDate, CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
