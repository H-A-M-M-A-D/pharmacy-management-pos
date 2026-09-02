using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.DTOs.Sales;

public sealed record PosProductSearchQuery(string? Q = null, Guid? BranchId = null, int Take = 20);

public sealed record PosProductDto(
    Guid ProductId, string Name, string SKU, string? Barcode, string? GenericName,
    string? BrandName, string Unit, int AvailableQuantity, DateOnly? NearestExpiryDate,
    decimal? IndicativeRetailPrice, decimal MaximumDiscountPercent, bool IsActive);

public sealed record SaleLineRequest(Guid ProductId, int Quantity, decimal DiscountPercent = 0);
public sealed record SalePaymentRequest(SalePaymentMethod Method, decimal AmountApplied, decimal? TenderedAmount = null, string? ReferenceNumber = null);

public sealed record HoldSaleRequest(
    Guid? BranchId, string? CustomerName, string? CustomerPhone, string? Notes,
    IReadOnlyList<SaleLineRequest> Items);

public sealed record PostSaleRequest(
    Guid? BranchId, string? CustomerName, string? CustomerPhone, string? Notes,
    IReadOnlyList<SaleLineRequest> Items, IReadOnlyList<SalePaymentRequest> Payments);

public sealed record PostHeldSaleRequest(IReadOnlyList<SalePaymentRequest> Payments);

public sealed record SalesHistoryQuery(
    int Page = 1, int PageSize = 25, string? Search = null, Guid? BranchId = null,
    Guid? CashierUserId = null, SaleStatus? Status = null, SalePaymentMethod? PaymentMethod = null,
    DateTime? FromUtc = null, DateTime? ToUtc = null);

public sealed record HeldSalesQuery(int Page = 1, int PageSize = 25, string? Search = null, Guid? BranchId = null);

public sealed record SaleListItemDto(
    Guid Id, string? InvoiceNumber, string? HoldNumber, SaleStatus Status, DateTime CreatedAt,
    DateTime? PostedAtUtc, Guid BranchId, string BranchName, string CashierName, string? CustomerName,
    string? CustomerPhone, int ItemCount, decimal NetTotal, decimal AmountPaid, decimal ChangeGiven,
    string PaymentSummary);

public sealed record SaleItemAllocationDto(
    Guid Id, Guid ProductBatchId, string BatchNumber, DateOnly ExpiryDate, int Quantity,
    decimal UnitRetailPriceSnapshot, decimal UnitSalePriceSnapshot, decimal GrossAmount,
    decimal DiscountAmount, decimal NetAmount);

public sealed record SaleItemDto(
    Guid Id, Guid ProductId, string ProductName, string SKU, int RequestedQuantity,
    decimal DiscountPercent, decimal GrossAmount, decimal DiscountAmount, decimal TaxAmount,
    decimal NetAmount, bool HasMixedBatchPricing, IReadOnlyList<SaleItemAllocationDto> Allocations);

public sealed record SalePaymentDto(Guid Id, SalePaymentMethod Method, decimal AmountApplied, decimal? TenderedAmount, string? ReferenceNumber);

public sealed record SaleDetailsDto(
    Guid Id, string? InvoiceNumber, string? HoldNumber, SaleStatus Status, DateTime CreatedAt,
    DateTime? PostedAtUtc, Guid BranchId, string BranchName, string? BranchAddress,
    string? BranchPhone, Guid CashierUserId, string CashierName, string? CustomerName,
    string? CustomerPhone, decimal Subtotal, decimal DiscountTotal, decimal TaxTotal,
    decimal NetTotal, decimal AmountPaid, decimal ChangeGiven, string? Notes,
    IReadOnlyList<SaleItemDto> Items, IReadOnlyList<SalePaymentDto> Payments);

public sealed record ReceiptDto(
    string InvoiceNumber, string BranchName, string? BranchAddress, string? BranchPhone,
    DateTime PostedAtUtc, string CashierName, string? CustomerName, decimal Subtotal,
    decimal DiscountTotal, decimal TaxTotal, decimal NetTotal, decimal AmountPaid,
    decimal ChangeGiven, IReadOnlyList<SaleItemDto> Items, IReadOnlyList<SalePaymentDto> Payments);
