using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.DTOs.Purchasing;

public sealed record PurchaseOrderRequest(
    Guid BranchId, Guid SupplierId, DateOnly OrderDate, DateOnly? ExpectedDate,
    string? SupplierReference, string? Notes, IReadOnlyList<PurchaseOrderItemRequest> Items);

public sealed record PurchaseOrderItemRequest(Guid ProductId, int OrderedQuantity, decimal? ExpectedPurchasePrice, string? Notes);

public sealed record PurchaseOrderListQuery(
    int Page = 1, int PageSize = 25, string? Search = null, Guid? BranchId = null,
    Guid? SupplierId = null, PurchaseOrderStatus? Status = null, DateOnly? DateFrom = null, DateOnly? DateTo = null);

public sealed record PurchaseOrderListItemDto(
    Guid Id, string OrderNumber, DateOnly OrderDate, DateOnly? ExpectedDate,
    Guid SupplierId, string SupplierName, Guid BranchId, string BranchName,
    int ItemCount, int OrderedQuantity, int ReceivedQuantity, PurchaseOrderStatus Status);

public sealed record PurchaseOrderItemDto(
    Guid Id, Guid ProductId, string ProductName, string SKU, int OrderedQuantity,
    int ReceivedQuantity, int RemainingQuantity, decimal? ExpectedPurchasePrice, string? Notes);

public sealed record PurchaseOrderDetailsDto(
    Guid Id, string OrderNumber, DateOnly OrderDate, DateOnly? ExpectedDate,
    Guid SupplierId, string SupplierName, Guid BranchId, string BranchName,
    string? SupplierReference, PurchaseOrderStatus Status, string? Notes,
    DateTime CreatedAt, DateTime UpdatedAt, IReadOnlyList<PurchaseOrderItemDto> Items);

public sealed record GoodsReceiptRequest(
    Guid BranchId, Guid SupplierId, Guid? PurchaseOrderId, string? SupplierInvoiceNumber,
    DateOnly ReceiptDate, string? Notes, IReadOnlyList<GoodsReceiptItemRequest> Items);

public sealed record GoodsReceiptItemRequest(
    Guid ProductId, Guid? PurchaseOrderItemId, string BatchNumber,
    DateOnly? ManufacturingDate, DateOnly ExpiryDate, int PurchasedQuantity,
    int BonusQuantity, decimal PurchasePrice, decimal RetailPrice,
    decimal DiscountPercent = 0, decimal TaxPercent = 0);

public sealed record PurchaseHistoryQuery(
    int Page = 1, int PageSize = 25, string? Search = null, Guid? BranchId = null,
    Guid? SupplierId = null, GoodsReceiptStatus? Status = null, DateOnly? DateFrom = null, DateOnly? DateTo = null);

public sealed record PurchaseHistoryItemDto(
    Guid Id, string GrnNumber, string? SupplierInvoiceNumber, DateOnly ReceiptDate,
    Guid SupplierId, string SupplierName, Guid BranchId, string BranchName,
    decimal Subtotal, decimal DiscountTotal, decimal TaxTotal, decimal NetTotal,
    GoodsReceiptStatus Status, string? ReceivedBy, PurchaseReturnState ReturnState = PurchaseReturnState.NoReturns);

public sealed record GoodsReceiptItemDto(
    Guid Id, Guid ProductId, string ProductName, string SKU, Guid? PurchaseOrderItemId,
    string BatchNumber, DateOnly? ManufacturingDate, DateOnly ExpiryDate,
    int PurchasedQuantity, int BonusQuantity, int InventoryQuantity,
    decimal PurchasePrice, decimal RetailPrice, decimal DiscountPercent,
    decimal DiscountAmount, decimal TaxPercent, decimal TaxAmount, decimal NetLineAmount);

public sealed record GoodsReceiptDetailsDto(
    Guid Id, string GrnNumber, string? SupplierInvoiceNumber, DateOnly ReceiptDate,
    Guid SupplierId, string SupplierName, Guid BranchId, string BranchName,
    string? PurchaseOrderNumber, GoodsReceiptStatus Status, decimal Subtotal,
    decimal DiscountTotal, decimal TaxTotal, decimal NetTotal, string? Notes,
    DateTime CreatedAt, DateTime UpdatedAt, IReadOnlyList<GoodsReceiptItemDto> Items);

public sealed record PurchasingOptionsDto(
    IReadOnlyList<PurchasingLookupDto> Branches,
    IReadOnlyList<PurchasingLookupDto> Suppliers,
    IReadOnlyList<PurchasingProductLookupDto> Products);

public sealed record PurchasingLookupDto(Guid Id, string Name);
public sealed record PurchasingProductLookupDto(Guid Id, string Name, string SKU, string? GenericName, string? Manufacturer, decimal PurchasePrice, decimal RetailPrice);

public enum PurchaseReturnState
{
    NoReturns = 0,
    PartiallyReturned = 1,
    FullyReturned = 2
}

public sealed record ReturnableGoodsReceiptDto(
    Guid Id, string GrnNumber, string? SupplierInvoiceNumber, DateOnly ReceiptDate,
    Guid SupplierId, string SupplierName, Guid BranchId, string BranchName,
    decimal NetTotal, PurchaseReturnState ReturnState, IReadOnlyList<ReturnableGoodsReceiptItemDto> Items);

public sealed record ReturnableGoodsReceiptItemDto(
    Guid Id, Guid ProductId, string ProductName, string SKU, Guid ProductBatchId,
    string BatchNumber, DateOnly ExpiryDate, bool IsBatchExpired, bool IsBatchDisposed,
    int PurchasedQuantity, int PaidReturnedQuantity, int PaidRemainingQuantity,
    int BonusQuantity, int BonusReturnedQuantity, int BonusRemainingQuantity,
    int CurrentPhysicalStock, decimal PurchasePrice, decimal GrossRemainingCredit,
    decimal DiscountRemainingCredit, decimal TaxRemainingCredit, decimal NetRemainingSupplierCredit,
    int MaxPhysicalReturnQuantity);

public sealed record PostPurchaseReturnRequest(
    PurchaseReturnReason Reason, string? Notes, IReadOnlyList<PurchaseReturnItemRequest> Items);

public sealed record PurchaseReturnItemRequest(Guid OriginalGoodsReceiptItemId, int PaidReturnQuantity, int BonusReturnQuantity);

public sealed record PurchaseReturnListQuery(
    int Page = 1, int PageSize = 25, string? Search = null, Guid? BranchId = null,
    Guid? SupplierId = null, PurchaseReturnReason? Reason = null, DateOnly? DateFrom = null, DateOnly? DateTo = null);

public sealed record PurchaseReturnListItemDto(
    Guid Id, string ReturnNumber, string OriginalGrnNumber, string? SupplierInvoiceNumber,
    DateTime ReturnDateUtc, Guid SupplierId, string SupplierName, Guid BranchId, string BranchName,
    int PaidQuantity, int BonusQuantity, int TotalQuantity, decimal NetSupplierCredit,
    PurchaseReturnStatus Status, PurchaseReturnReason Reason, string ProcessedByName);

public sealed record PurchaseReturnDetailsDto(
    Guid Id, string ReturnNumber, Guid OriginalGoodsReceiptId, string OriginalGrnNumber,
    string? SupplierInvoiceNumber, Guid SupplierId, string SupplierName, Guid BranchId, string BranchName,
    Guid ProcessedByUserId, string ProcessedByName, DateTime ReturnDateUtc,
    PurchaseReturnReason Reason, string? Notes, decimal GrossReturnAmount,
    decimal DiscountAdjustment, decimal TaxAdjustment, decimal NetSupplierCredit,
    PurchaseReturnStatus Status, IReadOnlyList<PurchaseReturnItemDto> Items);

public sealed record PurchaseReturnItemDto(
    Guid Id, Guid OriginalGoodsReceiptItemId, Guid ProductId, string ProductName, string SKU,
    Guid ProductBatchId, string BatchNumber, DateOnly ExpiryDate, int PaidReturnQuantity,
    int BonusReturnQuantity, int TotalQuantity, decimal PurchasePriceSnapshot,
    decimal GrossReturnAmount, decimal DiscountAdjustment, decimal TaxAdjustment, decimal NetSupplierCredit);
