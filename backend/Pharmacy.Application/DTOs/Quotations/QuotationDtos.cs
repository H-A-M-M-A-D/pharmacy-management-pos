using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.DTOs.Quotations;

public sealed record QuotationLineRequest(Guid ProductId, int Quantity, decimal DiscountPercent = 0, decimal? UnitPriceOverride = null);

public sealed record QuotationRequest(
    Guid? BranchId, Guid CustomerId, Guid? GodownId, Guid? PriceLevelId,
    DateOnly QuotationDate, DateOnly? ValidUntil, string? Notes,
    IReadOnlyList<QuotationLineRequest> Items);

public sealed record QuotationListQuery(
    int Page = 1, int PageSize = 25, string? Search = null, Guid? BranchId = null,
    Guid? CustomerId = null, SalesQuotationStatus? Status = null,
    DateOnly? FromDate = null, DateOnly? ToDate = null);

public sealed record QuotationListItemDto(
    Guid Id, string QuotationNumber, Guid CustomerId, string CustomerName, DateOnly QuotationDate,
    DateOnly? ValidUntil, SalesQuotationStatus Status, decimal NetTotal, string CreatedByName);

public sealed record QuotationItemDto(
    Guid Id, Guid ProductId, string ProductName, string SKU, int Quantity, decimal UnitPrice,
    decimal DiscountPercent, decimal GrossAmount, decimal DiscountAmount, decimal NetAmount);

public sealed record QuotationDetailsDto(
    Guid Id, string QuotationNumber, Guid BranchId, string BranchName, Guid? GodownId, string? GodownName,
    Guid CustomerId, string CustomerCode, string CustomerName, Guid? PriceLevelId, string? PriceLevelName,
    DateOnly QuotationDate, DateOnly? ValidUntil, SalesQuotationStatus Status, string? Notes,
    decimal Subtotal, decimal DiscountTotal, decimal NetTotal,
    Guid CreatedByUserId, string CreatedByName, Guid? ApprovedByUserId, string? ApprovedByName,
    Guid? ConvertedToSalesOrderId, string? ConvertedToSalesOrderNumber, Guid? ConvertedToSaleId, string? ConvertedToSaleInvoiceNumber,
    DateTime? SentAtUtc, DateTime? RespondedAtUtc, DateTime? CancelledAtUtc, string? CancellationReason,
    DateTime CreatedAt, IReadOnlyList<QuotationItemDto> Items);

public sealed record RejectQuotationRequest(string? Reason);
public sealed record CancelQuotationRequest(string Reason);
public sealed record ConvertQuotationToOrderRequest(DateOnly? ExpectedDeliveryDate);
