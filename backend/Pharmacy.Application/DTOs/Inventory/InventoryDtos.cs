using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.DTOs.Inventory;

public enum InventoryStockStatus { Healthy, LowStock, OutOfStock }
public enum BatchState { Active, NearExpiry, Expired, Depleted, Disposed }
public enum AdjustmentReason
{
    PhysicalCountCorrection,
    Damaged,
    Expired,
    Broken,
    Leakage,
    TheftOrLoss,
    Missing,
    DataEntryCorrection,
    Other
}

public sealed record OpeningStockRequest(
    Guid BranchId, Guid ProductId, string BatchNumber, DateOnly? ManufacturingDate,
    DateOnly ExpiryDate, int Quantity, decimal PurchasePrice, decimal RetailPrice,
    Guid? SupplierId, string? Notes);

public sealed record StockAdjustmentRequest(
    Guid BranchId, Guid ProductId, Guid ProductBatchId, int Quantity,
    AdjustmentReason Reason, string? Notes);

public sealed record StockCountRequest(
    Guid BranchId, Guid ProductId, Guid ProductBatchId, int PhysicalQuantity,
    AdjustmentReason Reason, string? Notes);

public sealed record DisposeExpiredStockRequest(Guid ProductBatchId, int Quantity, string Reason);

public sealed record InventoryListQuery(
    int Page = 1, int PageSize = 25, Guid? BranchId = null, string? Search = null,
    Guid? CategoryId = null, Guid? ManufacturerId = null, InventoryStockStatus? StockStatus = null,
    bool? IsProductActive = null, string SortBy = "productname", bool Descending = false);

public sealed record BatchListQuery(
    int Page = 1, int PageSize = 25, Guid? BranchId = null, Guid? ProductId = null,
    string? Search = null, DateOnly? ExpiryFrom = null, DateOnly? ExpiryTo = null,
    BatchState? State = null, bool HasStockOnly = false);

public sealed record StockMovementListQuery(
    int Page = 1, int PageSize = 25, Guid? BranchId = null, Guid? ProductId = null,
    Guid? ProductBatchId = null, StockMovementType? MovementType = null,
    DateTime? FromUtc = null, DateTime? ToUtc = null, Guid? UserId = null, string? Search = null);

public sealed record ExpiryQuery(Guid? BranchId = null, int? Days = null, DateOnly? From = null, DateOnly? To = null);

public sealed record InventoryListItemDto(
    Guid ProductId, string ProductName, string SKU, string? GenericName, string Category,
    string? Manufacturer, int QuantityInStock, int ReorderLevel, InventoryStockStatus StockStatus,
    int ActiveBatchCount, DateOnly? NearestExpiryDate, decimal EstimatedStockValue);

public sealed record BatchListItemDto(
    Guid BatchId, Guid ProductId, string ProductName, string SKU, string BatchNumber,
    Guid BranchId, string BranchName, DateOnly ExpiryDate, int QuantityAvailable,
    decimal PurchasePrice, decimal RetailPrice, decimal EstimatedStockValue, BatchState State);

public sealed record InventoryDetailsDto(
    Guid BranchId, string BranchName, Guid ProductId, string ProductName, string SKU,
    string? GenericName, string Category, string? Manufacturer, int QuantityInStock,
    int ReorderLevel, InventoryStockStatus StockStatus, DateOnly? NearestExpiryDate,
    decimal EstimatedStockValue, IReadOnlyList<BatchListItemDto> Batches);

public sealed record ExpiryListItemDto(
    Guid BatchId, string ProductName, string SKU, string BatchNumber, string BranchName,
    DateOnly ExpiryDate, int DaysRemaining, int QuantityAvailable, decimal PurchasePrice,
    decimal EstimatedStockValue, string? Supplier);

public sealed record StockMovementListItemDto(
    Guid Id, DateTime CreatedAt, string ProductName, string BatchNumber, string BranchName,
    StockMovementType MovementType, int Quantity, string? PerformedBy,
    string? ReferenceType, Guid? ReferenceId, string? Notes);

public sealed record FefoPreviewRequest(Guid BranchId, Guid ProductId, int Quantity, DateOnly? BusinessDate = null);
public sealed record FefoPreviewDto(IReadOnlyList<FefoPreviewItemDto> Allocations);
public sealed record FefoPreviewItemDto(Guid BatchId, string BatchNumber, int AllocatedQuantity, DateOnly ExpiryDate);

public sealed record InventoryIntegrityIssueDto(
    Guid BranchId, Guid ProductId, Guid ProductBatchId, int BatchQuantity,
    int InventoryQuantity, int LedgerQuantity, string Message);

public sealed record InventoryOptionsDto(
    IReadOnlyList<InventoryLookupDto> Branches,
    IReadOnlyList<InventoryLookupDto> Categories,
    IReadOnlyList<InventoryLookupDto> Manufacturers,
    IReadOnlyList<ProductLookupDto> Products,
    IReadOnlyList<InventoryLookupDto> Suppliers);

public sealed record InventoryLookupDto(Guid Id, string Name);
public sealed record ProductLookupDto(Guid Id, string Name, string SKU, string? Barcode, string? GenericName, bool IsActive);

public sealed record CreateStockCountSessionRequest(
    Guid BranchId, DateOnly CountDate, StockCountScope Scope, Guid? CategoryId,
    IReadOnlyList<Guid>? ProductIds, IReadOnlyList<Guid>? ProductBatchIds, string? Notes);

public sealed record SubmitStockCountEntryRequest(Guid StockCountItemId, int CountedQuantity, AdjustmentReason? Reason, string? Notes);
public sealed record SubmitStockCountEntriesRequest(IReadOnlyList<SubmitStockCountEntryRequest> Entries);
public sealed record CancelStockCountSessionRequest(string Reason);

public sealed record StockCountSessionListQuery(
    int Page = 1, int PageSize = 25, Guid? BranchId = null, StockCountStatus? Status = null,
    DateOnly? From = null, DateOnly? To = null);

public sealed record StockCountItemDto(
    Guid Id, Guid ProductId, string ProductName, string SKU, Guid ProductBatchId, string BatchNumber,
    DateOnly ExpiryDate, int SystemQuantity, int? CountedQuantity, int? Variance, decimal UnitCostSnapshot,
    decimal? VarianceValue, string? Reason, string? Notes, string? CountedBy, DateTime? CountedAtUtc);

public sealed record StockCountSessionDto(
    Guid Id, string CountNumber, Guid BranchId, string BranchName, DateOnly CountDate, StockCountStatus Status,
    StockCountScope Scope, Guid? CategoryId, string? CategoryName, string? Notes,
    string CreatedBy, string? StartedBy, DateTime? StartedAtUtc, string? CompletedBy, DateTime? CompletedAtUtc,
    string? CancelledBy, DateTime? CancelledAtUtc, int TotalItems, int CountedItems, int VarianceItems,
    IReadOnlyList<StockCountItemDto> Items);

public sealed record StockCountSessionListItemDto(
    Guid Id, string CountNumber, Guid BranchId, string BranchName, DateOnly CountDate, StockCountStatus Status,
    StockCountScope Scope, string? CategoryName, int TotalItems, int CountedItems, int VarianceItems,
    string CreatedBy, DateTime CreatedAt, DateTime? CompletedAtUtc);
