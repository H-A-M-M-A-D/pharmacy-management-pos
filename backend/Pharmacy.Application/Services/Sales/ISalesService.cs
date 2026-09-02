using Pharmacy.Application.DTOs.Sales;
using Pharmacy.Application.DTOs.Users;

namespace Pharmacy.Application.Services.Sales;

public interface ISalesService
{
    Task<IReadOnlyList<PosProductDto>> SearchProductsAsync(Guid actorId, PosProductSearchQuery query, CancellationToken cancellationToken = default);
    Task<SaleDetailsDto> HoldSaleAsync(Guid actorId, HoldSaleRequest request, CancellationToken cancellationToken = default);
    Task<SaleDetailsDto> UpdateHeldSaleAsync(Guid actorId, Guid id, HoldSaleRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<SaleListItemDto>> ListHeldSalesAsync(Guid actorId, HeldSalesQuery query, CancellationToken cancellationToken = default);
    Task<SaleDetailsDto> GetSaleAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default);
    Task CancelHeldSaleAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default);
    Task<SaleDetailsDto> PostSaleAsync(Guid actorId, PostSaleRequest request, CancellationToken cancellationToken = default);
    Task<SaleDetailsDto> PostHeldSaleAsync(Guid actorId, Guid id, PostHeldSaleRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<SaleListItemDto>> ListSalesAsync(Guid actorId, SalesHistoryQuery query, CancellationToken cancellationToken = default);
    Task<ReceiptDto> ReceiptAsync(Guid actorId, Guid id, bool auditReprint, CancellationToken cancellationToken = default);
}
