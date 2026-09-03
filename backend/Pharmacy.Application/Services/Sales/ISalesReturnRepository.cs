using System.Data;
using Pharmacy.Application.DTOs.Sales;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Domain.Entities;
using DomainInventory = Pharmacy.Domain.Entities.Inventory;

namespace Pharmacy.Application.Services.Sales;

public interface ISalesReturnRepository
{
    Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<Sale?> GetOriginalSaleAsync(Guid saleId, CancellationToken cancellationToken = default);
    Task<ProductBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<DomainInventory?> GetInventoryAsync(Guid branchId, Guid productId, Guid batchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, int>> GetReturnedQuantitiesAsync(IEnumerable<Guid> allocationIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, decimal>> GetRefundedAmountsAsync(IEnumerable<Guid> allocationIds, CancellationToken cancellationToken = default);
    Task<string> NextReturnNumberAsync(DateTime returnDateUtc, CancellationToken cancellationToken = default);
    Task AddSalesReturnAsync(SalesReturn salesReturn, CancellationToken cancellationToken = default);
    Task AddMovementAsync(StockMovement movement, CancellationToken cancellationToken = default);
    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default);
    Task<PagedResult<SalesReturnListItemDto>> ListReturnsAsync(SalesReturnsQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task<SalesReturnDetailsDto?> GetReturnDetailsAsync(Guid id, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task<ReturnableSaleDto?> GetReturnableSaleAsync(Guid saleId, Guid? actorBranchId, bool canSelectBranch, DateOnly businessDate, CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
