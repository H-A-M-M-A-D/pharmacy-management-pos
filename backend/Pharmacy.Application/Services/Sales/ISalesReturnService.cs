using Pharmacy.Application.DTOs.Sales;
using Pharmacy.Application.DTOs.Users;

namespace Pharmacy.Application.Services.Sales;

public interface ISalesReturnService
{
    Task<ReturnableSaleDto> GetReturnableSaleAsync(Guid actorId, Guid saleId, CancellationToken cancellationToken = default);
    Task<SalesReturnDetailsDto> PostReturnAsync(Guid actorId, Guid saleId, PostSalesReturnRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<SalesReturnListItemDto>> ListReturnsAsync(Guid actorId, SalesReturnsQuery query, CancellationToken cancellationToken = default);
    Task<SalesReturnDetailsDto> GetReturnAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default);
    Task<SalesReturnReceiptDto> ReceiptAsync(Guid actorId, Guid id, bool auditReprint, CancellationToken cancellationToken = default);
}
