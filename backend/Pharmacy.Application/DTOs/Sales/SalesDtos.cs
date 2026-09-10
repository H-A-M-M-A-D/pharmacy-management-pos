using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.DTOs.Sales;

public sealed record PosProductSearchQuery(string? Q = null, Guid? BranchId = null, int Take = 20, Guid? GodownId = null);

public sealed record PosProductDto(
    Guid ProductId, string Name, string SKU, string? Barcode, string? GenericName,
    string? BrandName, string Unit, int AvailableQuantity, DateOnly? NearestExpiryDate,
    decimal? IndicativeRetailPrice, decimal MaximumDiscountPercent, bool IsActive);

public sealed record SaleLineRequest(Guid ProductId, int Quantity, decimal DiscountPercent = 0);
public sealed record SalePaymentRequest(SalePaymentMethod Method, decimal AmountApplied, decimal? TenderedAmount = null, string? ReferenceNumber = null, Guid? FinancialAccountId = null);

public sealed record HoldSaleRequest(
    Guid? BranchId, string? CustomerName, string? CustomerPhone, string? Notes,
    IReadOnlyList<SaleLineRequest> Items, Guid? GodownId = null);

public sealed record PostSaleRequest(
    Guid? BranchId, Guid? CustomerId, string? CustomerName, string? CustomerPhone, string? Notes,
    IReadOnlyList<SaleLineRequest> Items, IReadOnlyList<SalePaymentRequest> Payments, Guid? GodownId = null);

public sealed record PostHeldSaleRequest(Guid? CustomerId, IReadOnlyList<SalePaymentRequest> Payments);

public sealed record SalesHistoryQuery(
    int Page = 1, int PageSize = 25, string? Search = null, Guid? BranchId = null,
    Guid? CashierUserId = null, SaleStatus? Status = null, SalePaymentMethod? PaymentMethod = null,
    DateTime? FromUtc = null, DateTime? ToUtc = null);

public sealed record HeldSalesQuery(int Page = 1, int PageSize = 25, string? Search = null, Guid? BranchId = null);

public sealed record SaleListItemDto(
    Guid Id, string? InvoiceNumber, string? HoldNumber, SaleStatus Status, DateTime CreatedAt,
    DateTime? PostedAtUtc, Guid BranchId, string BranchName, string CashierName, string? CustomerName,
    string? CustomerPhone, int ItemCount, decimal NetTotal, decimal AmountPaid, decimal ChangeGiven,
    decimal CreditAmount, string PaymentSummary, SalesReturnState ReturnState = SalesReturnState.NotReturned);

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
    string? BranchPhone, Guid CashierUserId, string CashierName, Guid? CustomerId,
    string? CustomerCode, string? CustomerName, string? CustomerPhone, decimal Subtotal,
    decimal DiscountTotal, decimal TaxTotal, decimal NetTotal, decimal AmountPaid,
    decimal CreditAmount, decimal ChangeGiven, string? Notes,
    IReadOnlyList<SaleItemDto> Items, IReadOnlyList<SalePaymentDto> Payments);

public sealed record ReceiptDto(
    string InvoiceNumber, string BranchName, string? BranchAddress, string? BranchPhone,
    DateTime PostedAtUtc, string CashierName, string? CustomerName, decimal Subtotal,
    decimal DiscountTotal, decimal TaxTotal, decimal NetTotal, decimal AmountPaid,
    decimal CreditAmount, decimal ChangeGiven, IReadOnlyList<SaleItemDto> Items, IReadOnlyList<SalePaymentDto> Payments);

public sealed record ReturnableSaleDto(
    Guid SaleId, string InvoiceNumber, DateTime PostedAtUtc, Guid BranchId, string BranchName,
    string CashierName, string? CustomerName, string? CustomerPhone, decimal NetTotal,
    Guid? CustomerId, string? CustomerCode, decimal AmountPaid, decimal CreditAmount,
    SalesReturnState ReturnState, IReadOnlyList<ReturnableSaleItemDto> Items,
    IReadOnlyList<SalePaymentDto> OriginalPayments);

public sealed record ReturnableSaleItemDto(
    Guid SaleItemId, Guid ProductId, string ProductName, string SKU, int SoldQuantity,
    int AlreadyReturnedQuantity, int RemainingQuantity, decimal OriginalNetAmount,
    decimal RemainingRefundAmount, IReadOnlyList<ReturnableAllocationDto> Allocations);

public sealed record ReturnableAllocationDto(
    Guid AllocationId, Guid ProductBatchId, string BatchNumber, DateOnly ExpiryDate,
    int OriginalQuantity, int AlreadyReturnedQuantity, int RemainingQuantity,
    decimal UnitSalePriceSnapshot, decimal RefundRemaining, bool IsBatchDisposed,
    bool IsBatchExpired);

public sealed record SalesReturnAllocationRequest(Guid OriginalAllocationId, int Quantity, SalesReturnDisposition Disposition);
public sealed record SalesRefundPaymentRequest(SalePaymentMethod Method, decimal Amount, string? ReferenceNumber = null, Guid? FinancialAccountId = null);
public sealed record PostSalesReturnRequest(SalesReturnReason Reason, string? Notes, IReadOnlyList<SalesReturnAllocationRequest> Allocations, IReadOnlyList<SalesRefundPaymentRequest> RefundPayments);

public sealed record SalesReturnsQuery(
    int Page = 1, int PageSize = 25, string? Search = null, Guid? BranchId = null,
    Guid? ProcessedByUserId = null, SalesReturnReason? Reason = null,
    DateTime? FromUtc = null, DateTime? ToUtc = null);

public sealed record SalesReturnListItemDto(
    Guid Id, string ReturnNumber, string OriginalInvoiceNumber, DateTime ReturnDateUtc,
    Guid BranchId, string BranchName, string ProcessedByName, string? CustomerName,
    int ItemCount, decimal RefundAmount, SalesReturnStatus Status, SalesReturnReason Reason);

public sealed record SalesReturnAllocationDto(
    Guid Id, Guid OriginalAllocationId, Guid ProductBatchId, string BatchNumber,
    DateOnly ExpiryDate, int Quantity, SalesReturnDisposition Disposition,
    decimal UnitSalePriceSnapshot, decimal GrossReturnAmount, decimal DiscountReturnAmount,
    decimal TaxReturnAmount, decimal RefundAmount);

public sealed record SalesReturnItemDto(
    Guid Id, Guid OriginalSaleItemId, Guid ProductId, string ProductName, string SKU,
    int Quantity, decimal GrossReturnAmount, decimal DiscountReturnAmount,
    decimal TaxReturnAmount, decimal RefundAmount, IReadOnlyList<SalesReturnAllocationDto> Allocations);

public sealed record SalesRefundPaymentDto(Guid Id, SalePaymentMethod Method, decimal Amount, string? ReferenceNumber);

public sealed record SalesReturnDetailsDto(
    Guid Id, string ReturnNumber, Guid OriginalSaleId, string OriginalInvoiceNumber,
    Guid BranchId, string BranchName, string? BranchAddress, string? BranchPhone,
    Guid ProcessedByUserId, string ProcessedByName, DateTime ReturnDateUtc,
    SalesReturnReason Reason, string? Notes, decimal GrossReturnAmount,
    decimal DiscountReturnAmount, decimal TaxReturnAmount, decimal RefundAmount,
    decimal CustomerCreditReductionAmount, decimal CashRefundAmount,
    SalesReturnStatus Status, string? CustomerName, string? CustomerPhone,
    IReadOnlyList<SalesReturnItemDto> Items, IReadOnlyList<SalesRefundPaymentDto> RefundPayments);

public sealed record SalesReturnReceiptDto(
    string ReturnNumber, string OriginalInvoiceNumber, string BranchName, string? BranchAddress,
    string? BranchPhone, DateTime ReturnDateUtc, string ProcessedByName, string? CustomerName,
    SalesReturnReason Reason, decimal RefundAmount, decimal CustomerCreditReductionAmount,
    decimal CashRefundAmount, IReadOnlyList<SalesReturnItemDto> Items,
    IReadOnlyList<SalesRefundPaymentDto> RefundPayments);

public enum SalesReturnState
{
    NotReturned = 1,
    PartiallyReturned = 2,
    FullyReturned = 3
}
