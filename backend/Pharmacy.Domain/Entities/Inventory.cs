using Pharmacy.Domain.Entities.Base;

namespace Pharmacy.Domain.Entities;

/// <summary>
/// Represents current inventory snapshot for a product batch at a branch.
/// This is a convenience/cache table for quick queries.
/// The source of truth is StockMovement history.
/// </summary>
public class Inventory : Entity
{
    /// <summary>
    /// Foreign key to branch.
    /// </summary>
    public Guid BranchId { get; set; }

    /// <summary>
    /// Navigation property to branch.
    /// </summary>
    public Branch? Branch { get; set; }

    /// <summary>
    /// Foreign key to product.
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Navigation property to product.
    /// </summary>
    public Product? Product { get; set; }

    /// <summary>
    /// Foreign key to product batch.
    /// </summary>
    public Guid ProductBatchId { get; set; }

    /// <summary>
    /// Navigation property to product batch.
    /// </summary>
    public ProductBatch? ProductBatch { get; set; }

    /// <summary>
    /// Current quantity in stock (derived from StockMovement history).
    /// </summary>
    public int QuantityInStock { get; set; }

    /// <summary>
    /// Minimum stock threshold/reorder level.
    /// </summary>
    public int ReorderLevel { get; set; }

    /// <summary>
    /// Last stock count/update timestamp.
    /// </summary>
    public DateTime LastCountedAt { get; set; }
}
