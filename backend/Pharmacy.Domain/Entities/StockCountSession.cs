using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// A physical stock-take session for a branch. Header for a batch of <see cref="StockCountItem"/> lines.
/// Finalizing a session posts one stock movement per counted line with a non-zero variance.
/// </summary>
public class StockCountSession : Entity
{
    public required string CountNumber { get; set; }
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    /// <summary>Godown this count is scoped to. Null means the count spans the whole branch (all godowns),
    /// preserved for legacy/unmigrated branches and branch-wide counts alike.</summary>
    public Guid? GodownId { get; set; }
    public Godown? Godown { get; set; }
    public DateOnly CountDate { get; set; }
    public StockCountStatus Status { get; set; } = StockCountStatus.Draft;
    public StockCountScope Scope { get; set; }
    public Guid? CategoryId { get; set; }
    public ProductCategory? Category { get; set; }
    public string? Notes { get; set; }

    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public Guid? StartedByUserId { get; set; }
    public User? StartedByUser { get; set; }
    public DateTime? StartedAtUtc { get; set; }

    public Guid? CompletedByUserId { get; set; }
    public User? CompletedByUser { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public Guid? CancelledByUserId { get; set; }
    public User? CancelledByUser { get; set; }
    public DateTime? CancelledAtUtc { get; set; }

    public ICollection<StockCountItem> Items { get; set; } = [];
}

/// <summary>
/// A single product/batch line within a stock count session, snapshotting the system quantity
/// at count time so the physical count can be compared against it even if stock keeps moving.
/// </summary>
public class StockCountItem : Entity
{
    public Guid StockCountSessionId { get; set; }
    public StockCountSession? StockCountSession { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid ProductBatchId { get; set; }
    public ProductBatch? ProductBatch { get; set; }

    public int SystemQuantity { get; set; }
    public int? CountedQuantity { get; set; }
    public decimal UnitCostSnapshot { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }

    public Guid? CountedByUserId { get; set; }
    public User? CountedByUser { get; set; }
    public DateTime? CountedAtUtc { get; set; }
}

public enum StockCountStatus
{
    Draft = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

public enum StockCountScope
{
    Full = 1,
    Category = 2,
    SelectedProducts = 3,
    SelectedBatches = 4
}
