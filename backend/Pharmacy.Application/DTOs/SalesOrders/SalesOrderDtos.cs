using Pharmacy.Application.DTOs.Sales;
using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.DTOs.SalesOrders;

public sealed record SalesOrderLineRequest(Guid ProductId, int Quantity, decimal DiscountPercent = 0, decimal? UnitPriceOverride = null, string? PriceOverrideReason = null, string? DiscountOverrideReason = null);

public sealed record SalesOrderRequest(
    Guid? BranchId, Guid CustomerId, Guid? GodownId, Guid? PriceLevelId,
    DateOnly OrderDate, DateOnly? ExpectedDeliveryDate, string? Notes,
    IReadOnlyList<SalesOrderLineRequest> Items);

public sealed record SalesOrderListQuery(
    int Page = 1, int PageSize = 25, string? Search = null, Guid? BranchId = null,
    Guid? CustomerId = null, SalesOrderStatus? Status = null,
    DateOnly? FromDate = null, DateOnly? ToDate = null);

public sealed record SalesOrderListItemDto(
    Guid Id, string OrderNumber, Guid CustomerId, string CustomerName, DateOnly OrderDate,
    SalesOrderStatus Status, decimal NetTotal, int OrderedQuantity, int FulfilledQuantity);

public sealed record SalesOrderItemDto(
    Guid Id, Guid ProductId, string ProductName, string SKU, int OrderedQuantity, int FulfilledQuantity,
    decimal UnitPrice, decimal DiscountPercent, decimal GrossAmount, decimal DiscountAmount, decimal NetAmount);

public sealed record LinkedSaleDto(Guid SaleId, string? InvoiceNumber, DateTime? PostedAtUtc, decimal NetTotal);

public sealed record SalesOrderDetailsDto(
    Guid Id, string OrderNumber, Guid BranchId, string BranchName, Guid? GodownId, string? GodownName,
    Guid CustomerId, string CustomerCode, string CustomerName, Guid? PriceLevelId, string? PriceLevelName,
    Guid? QuotationId, string? QuotationNumber,
    DateOnly OrderDate, DateOnly? ExpectedDeliveryDate, SalesOrderStatus Status, string? Notes,
    decimal Subtotal, decimal DiscountTotal, decimal NetTotal,
    Guid CreatedByUserId, string CreatedByName, Guid? ConfirmedByUserId, string? ConfirmedByName, DateTime? ConfirmedAtUtc,
    DateTime? CancelledAtUtc, string? CancellationReason, DateTime CreatedAt,
    IReadOnlyList<SalesOrderItemDto> Items, IReadOnlyList<LinkedSaleDto> LinkedSales);

public sealed record CancelSalesOrderRequest(string Reason);

public sealed record FulfillSalesOrderLineRequest(Guid ProductId, int Quantity);

public sealed record FulfillSalesOrderRequest(
    IReadOnlyList<FulfillSalesOrderLineRequest> Items, IReadOnlyList<SalePaymentRequest> Payments,
    string? CustomerPoNumber = null, DateOnly? DueDateOverride = null, string? CreditLimitOverrideReason = null);
