using Pharmacy.Application.DTOs.SalesOrders;
using Pharmacy.Application.DTOs.Sales;
using Pharmacy.Application.DTOs.Users;

namespace Pharmacy.Application.Services.SalesOrders;

public interface ISalesOrderService
{
    Task<PagedResult<SalesOrderListItemDto>> ListOrdersAsync(Guid actorId, SalesOrderListQuery query, CancellationToken cancellationToken = default);
    Task<SalesOrderDetailsDto> GetOrderAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default);
    Task<SalesOrderDetailsDto> CreateOrderAsync(Guid actorId, SalesOrderRequest request, CancellationToken cancellationToken = default);
    Task<SalesOrderDetailsDto> UpdateOrderAsync(Guid actorId, Guid id, SalesOrderRequest request, CancellationToken cancellationToken = default);
    Task<SalesOrderDetailsDto> ConfirmOrderAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default);
    Task<SalesOrderDetailsDto> CancelOrderAsync(Guid actorId, Guid id, CancelSalesOrderRequest request, CancellationToken cancellationToken = default);
    Task<SaleDetailsDto> FulfillOrderAsync(Guid actorId, Guid id, FulfillSalesOrderRequest request, CancellationToken cancellationToken = default);
}
