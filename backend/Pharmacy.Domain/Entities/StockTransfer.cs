using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// Header for an inter-godown stock transfer, possibly crossing branches. Individual line quantities
/// (requested/approved/dispatched/received) live on <see cref="StockTransferItem"/>; this header only
/// tracks the workflow lifecycle. StockMovement remains the source of truth for actual stock changes -
/// dispatch/receive create TransferOut/TransferIn rows, this header is a coordinating document.
/// </summary>
public class StockTransfer : Entity
{
    public required string TransferNumber { get; set; }

    public Guid SourceBranchId { get; set; }
    public Branch? SourceBranch { get; set; }
    public Guid SourceGodownId { get; set; }
    public Godown? SourceGodown { get; set; }

    public Guid DestinationBranchId { get; set; }
    public Branch? DestinationBranch { get; set; }
    public Guid DestinationGodownId { get; set; }
    public Godown? DestinationGodown { get; set; }

    public DateOnly TransferDate { get; set; }
    public StockTransferStatus Status { get; set; } = StockTransferStatus.Draft;
    public string? Notes { get; set; }

    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public Guid? RequestedByUserId { get; set; }
    public User? RequestedByUser { get; set; }
    public DateTime? RequestedAtUtc { get; set; }

    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    public Guid? DispatchedByUserId { get; set; }
    public User? DispatchedByUser { get; set; }
    public DateTime? DispatchedAtUtc { get; set; }

    /// <summary>Set by the most recent receiving event (full or partial). See StockMovement TransferIn
    /// rows referencing this transfer for the full, immutable per-event receiving history.</summary>
    public Guid? ReceivedByUserId { get; set; }
    public User? ReceivedByUser { get; set; }
    public DateTime? ReceivedAtUtc { get; set; }

    public Guid? CancelledByUserId { get; set; }
    public User? CancelledByUser { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }

    public ICollection<StockTransferItem> Items { get; set; } = new List<StockTransferItem>();
}

/// <summary>
/// A single batch-level line on a stock transfer. Transfers are always requested and dispatched at the
/// batch level (never a vague product quantity) so batch identity - number, expiry, cost - is preserved
/// end to end. <see cref="DestinationProductBatchId"/> links to the batch actually credited at the
/// destination godown once received, for source-to-destination traceability.
/// </summary>
public class StockTransferItem : Entity
{
    public Guid StockTransferId { get; set; }
    public StockTransfer? StockTransfer { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public Guid SourceProductBatchId { get; set; }
    public ProductBatch? SourceProductBatch { get; set; }

    public Guid? DestinationProductBatchId { get; set; }
    public ProductBatch? DestinationProductBatch { get; set; }

    /// <summary>Batch number snapshot at request time, independent of any later change to the source batch.</summary>
    public required string BatchNumber { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public decimal UnitCostSnapshot { get; set; }

    public int QuantityRequested { get; set; }
    public int QuantityApproved { get; set; }
    public int QuantityDispatched { get; set; }
    public int QuantityReceived { get; set; }

    public string? Notes { get; set; }
}

public enum StockTransferStatus
{
    Draft = 1,
    Requested = 2,
    Approved = 3,
    Dispatched = 4,
    PartiallyReceived = 5,
    Received = 6,
    Cancelled = 7
}
