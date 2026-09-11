using System.Data;
using Pharmacy.Application.DTOs.SalesOrders;
using Pharmacy.Application.DTOs.Users;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.Services.SalesOrders;

public interface ISalesOrderRepository
{
    Task<User?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<Branch?> GetBranchAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<Product?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<SalesOrder?> GetOrderAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Tracked load (order + items) used both for the service's own mutating actions and,
    /// via ISalesOrderRepository, by SalesService when fulfilling an order into a Sale within the
    /// same atomic transaction.</summary>
    Task<SalesOrder?> GetOrderForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<string> NextOrderNumberAsync(CancellationToken cancellationToken = default);
    Task AddOrderAsync(SalesOrder order, CancellationToken cancellationToken = default);
    void ReplaceOrderItems(SalesOrder order, List<SalesOrderItem> items);
    Task<IReadOnlyList<LinkedSaleDto>> GetLinkedSalesAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<SalesOrderDetailsDto?> GetOrderDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<SalesOrderListItemDto>> ListOrdersAsync(SalesOrderListQuery query, Guid? actorBranchId, bool canSelectBranch, CancellationToken cancellationToken = default);
    Task AddAuditAsync(AuditLog audit, CancellationToken cancellationToken = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
