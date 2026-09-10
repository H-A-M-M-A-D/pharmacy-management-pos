using Pharmacy.Domain.Entities;

namespace Pharmacy.Application.DTOs.StockTransfers;

public sealed record StockTransferItemRequest(Guid ProductId, Guid ProductBatchId, int QuantityRequested, string? Notes);

public sealed record StockTransferRequest(
    Guid SourceBranchId, Guid SourceGodownId, Guid DestinationBranchId, Guid DestinationGodownId,
    DateOnly TransferDate, string? Notes, IReadOnlyList<StockTransferItemRequest> Items);

public sealed record ApproveStockTransferItemRequest(Guid StockTransferItemId, int QuantityApproved);
public sealed record ApproveStockTransferRequest(IReadOnlyList<ApproveStockTransferItemRequest>? Items);

public sealed record DispatchStockTransferItemRequest(Guid StockTransferItemId, int QuantityDispatched);
public sealed record DispatchStockTransferRequest(IReadOnlyList<DispatchStockTransferItemRequest>? Items);

public sealed record ReceiveStockTransferItemRequest(Guid StockTransferItemId, int QuantityReceived, string? Notes = null);
public sealed record ReceiveStockTransferRequest(IReadOnlyList<ReceiveStockTransferItemRequest> Items, string? Notes);

public sealed record CancelStockTransferRequest(string Reason);
public sealed record ResolveStockTransferDiscrepancyRequest(string Reason);

public sealed record StockTransferListQuery(
    int Page = 1, int PageSize = 25, string? TransferNumber = null, DateOnly? DateFrom = null, DateOnly? DateTo = null,
    StockTransferStatus? Status = null, Guid? SourceBranchId = null, Guid? SourceGodownId = null,
    Guid? DestinationBranchId = null, Guid? DestinationGodownId = null, Guid? ProductId = null);

public sealed record StockTransferListItemDto(
    Guid Id, string TransferNumber, DateOnly TransferDate, StockTransferStatus Status,
    Guid SourceBranchId, string SourceBranchName, Guid SourceGodownId, string SourceGodownName,
    Guid DestinationBranchId, string DestinationBranchName, Guid DestinationGodownId, string DestinationGodownName,
    int QuantityRequested, int QuantityApproved, int QuantityDispatched, int QuantityReceived, int QuantityInTransit,
    string? RequestedBy, DateTime CreatedAt);

public sealed record StockTransferItemDto(
    Guid Id, Guid ProductId, string ProductName, string SKU,
    Guid SourceProductBatchId, string BatchNumber, DateOnly ExpiryDate, decimal UnitCostSnapshot,
    Guid? DestinationProductBatchId,
    int QuantityRequested, int QuantityApproved, int QuantityDispatched, int QuantityReceived, int QuantityInTransit,
    string? Notes);

public sealed record StockTransferDetailsDto(
    Guid Id, string TransferNumber, DateOnly TransferDate, StockTransferStatus Status, string? Notes,
    Guid SourceBranchId, string SourceBranchName, Guid SourceGodownId, string SourceGodownName,
    Guid DestinationBranchId, string DestinationBranchName, Guid DestinationGodownId, string DestinationGodownName,
    string? CreatedBy, DateTime CreatedAt,
    string? RequestedBy, DateTime? RequestedAtUtc,
    string? ApprovedBy, DateTime? ApprovedAtUtc,
    string? DispatchedBy, DateTime? DispatchedAtUtc,
    string? ReceivedBy, DateTime? ReceivedAtUtc,
    string? CancelledBy, DateTime? CancelledAtUtc, string? CancellationReason,
    IReadOnlyList<StockTransferItemDto> Items);

public sealed record TransferableBatchDto(
    Guid ProductBatchId, Guid ProductId, string ProductName, string SKU, string BatchNumber,
    DateOnly ExpiryDate, int QuantityAvailable, decimal PurchasePrice, decimal RetailPrice);
