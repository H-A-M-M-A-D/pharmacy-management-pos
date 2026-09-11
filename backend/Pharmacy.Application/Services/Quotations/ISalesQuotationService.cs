using Pharmacy.Application.DTOs.Quotations;
using Pharmacy.Application.DTOs.Sales;
using Pharmacy.Application.DTOs.SalesOrders;
using Pharmacy.Application.DTOs.Users;

namespace Pharmacy.Application.Services.Quotations;

public interface ISalesQuotationService
{
    Task<PagedResult<QuotationListItemDto>> ListQuotationsAsync(Guid actorId, QuotationListQuery query, CancellationToken cancellationToken = default);
    Task<QuotationDetailsDto> GetQuotationAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default);
    Task<QuotationDetailsDto> CreateQuotationAsync(Guid actorId, QuotationRequest request, CancellationToken cancellationToken = default);
    Task<QuotationDetailsDto> UpdateQuotationAsync(Guid actorId, Guid id, QuotationRequest request, CancellationToken cancellationToken = default);
    Task<QuotationDetailsDto> SendQuotationAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default);
    Task<QuotationDetailsDto> AcceptQuotationAsync(Guid actorId, Guid id, CancellationToken cancellationToken = default);
    Task<QuotationDetailsDto> RejectQuotationAsync(Guid actorId, Guid id, RejectQuotationRequest request, CancellationToken cancellationToken = default);
    Task<QuotationDetailsDto> CancelQuotationAsync(Guid actorId, Guid id, CancelQuotationRequest request, CancellationToken cancellationToken = default);
    Task<SalesOrderDetailsDto> ConvertToSalesOrderAsync(Guid actorId, Guid id, ConvertQuotationToOrderRequest request, CancellationToken cancellationToken = default);
    Task<SaleDetailsDto> ConvertToSaleAsync(Guid actorId, Guid id, IReadOnlyList<SalePaymentRequest> payments, CancellationToken cancellationToken = default);
}
